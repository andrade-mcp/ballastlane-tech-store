# Deploy — Oracle Cloud (consultinfra2 / instance-20260402-2351)

Production runbook for deploying tech-store to the Oracle ARM VM. Designed to
co-tenant cleanly with the existing `linkedin-monitor` service on the same VM.

**Currently deployed at:** <https://ballastlane-tech.store>
(`auth.ballastlane-tech.store`, `api.ballastlane-tech.store`)

## Target

| Field | Value |
|---|---|
| VM | `instance-20260402-2351` (consultinfra2 tenancy) |
| Public IP | `168.138.155.229` |
| Private IP | `172.20.10.89` |
| Shape | `VM.Standard.A1.Flex` — 4 OCPU / 24 GB / ARM64 |
| OS | Ubuntu 22.04 Minimal aarch64 |
| SSH | `ssh -i <key> ubuntu@168.138.155.229` |
| Existing service | `linkedin-monitor` on `:8000` (do not touch) |
| Existing tunnel | `cloudflared-tunnel.service` (extend, don't replace) |

The SSH private key lives at
`C:/Repos/PRD/linkedin-monitor/docs/infra/OCI/ssh-key-2026-04-03.key`.

## Topology after deploy

```
Cloudflare DNS                       Cloudflare Tunnel             Docker on the VM
(zone: ballastlane-tech.store)        (linkedin-hunter)             127.0.0.1 only
──────────────────────────────       ─────────────────────         ─────────────────────
ballastlane-tech.store          ──▶ http://localhost:5174  ──▶ web (nginx + React build)
auth.ballastlane-tech.store     ──▶ http://localhost:5101  ──▶ auth-api (.NET 9)
api.ballastlane-tech.store      ──▶ http://localhost:5102  ──▶ store-api (.NET 9)
                                                              ──▶ postgres (no host port)
```

Nothing in the tech-store stack publishes a port to `0.0.0.0` — every binding is
`127.0.0.1`, and only `cloudflared` (running on the host) reaches them. The
existing linkedin-monitor on `:8000` is untouched.

## Prerequisites

On your laptop:

- SSH key added to your agent: `ssh-add <path-to-ssh-key-2026-04-03.key>`
- `gh` CLI authenticated as `andrade-mcp` (already done — only used if you want to
  pull a private branch via HTTPS over PAT)
- `cloudflared` CLI on the VM (already installed for linkedin-monitor)

## First-time setup

### 0. Move `ballastlane-tech.store` to Cloudflare DNS

The tunnel can only route hostnames in zones owned by the same Cloudflare account
that owns the tunnel cert. The domain ships from Namecheap.

1. Cloudflare dashboard → **Add a site** → enter `ballastlane-tech.store` → choose
   the **Free** plan. (Or use the API token — see "Bootstrap from scratch" at the
   end of this file.)
2. Cloudflare assigns two nameservers per zone — for this domain they were
   `keenan.ns.cloudflare.com` and `melina.ns.cloudflare.com`. **The names are
   per-zone**, so don't copy these blindly into another deploy; use whatever
   Cloudflare assigns when you add the site.
3. Namecheap dashboard → **Domain List** → **Manage** → **Nameservers** → switch
   to **Custom DNS** → paste the two assigned names → save.
4. Propagation usually completes in 5–30 minutes. Verify with:
   ```bash
   curl -sH 'Accept: application/dns-json' \
     'https://cloudflare-dns.com/dns-query?name=ballastlane-tech.store&type=NS'
   ```
   When `Status: 0` and the `Answer` block lists the two assigned names, you're
   propagated.
5. SSL/TLS mode defaults to `Full` for new zones via the API, which is what we
   want — Cloudflare terminates TLS at the edge and talks plain HTTP through the
   tunnel to the origin.

Once the zone shows **Active**, the universal SSL certificate provisions
automatically (1–15 min). `https://<host>` returns SSL handshake errors until
the cert lands.

### 1. SSH to the VM

```bash
ssh -i ssh-key-2026-04-03.key ubuntu@168.138.155.229
```

### 2. Bootstrap

```bash
curl -fsSL https://raw.githubusercontent.com/andrade-mcp/ballastlane-tech-store/main/deploy/bootstrap.sh \
  | bash
```

This installs Docker if missing, clones the repo into
`/home/ubuntu/ballastlane-tech-store`, drops a skeleton `.env`, and grants
passwordless sudo for `docker` / `docker compose` so future deploys don't prompt.

> The repo is private. If `bootstrap.sh` cannot clone over HTTPS, clone over SSH
> using a deploy key:
>
> ```bash
> ssh-keygen -t ed25519 -C "instance-20260402-2351-techstore-deploy" -f ~/.ssh/techstore_deploy
> # add ~/.ssh/techstore_deploy.pub as a deploy key on the GitHub repo
> GIT_SSH_COMMAND='ssh -i ~/.ssh/techstore_deploy -o StrictHostKeyChecking=no' \
>   git clone git@github.com:andrade-mcp/ballastlane-tech-store.git \
>   /home/ubuntu/ballastlane-tech-store
> ```

### 3. Generate secrets and edit `.env`

```bash
cd /home/ubuntu/ballastlane-tech-store
nano .env
```

Generate a strong JWT signing key:

```bash
openssl rand -base64 48 | tr -d '\n'
```

Paste it into `JWT_SIGNING_KEY` and confirm the public origins match the hostnames
configured below.

### 4. Configure Cloudflare DNS + tunnel ingress

Route the three hostnames to the existing `linkedin-hunter` tunnel. Run on the VM
(after step 0 has the zone Active in Cloudflare):

```bash
TUNNEL=$(cloudflared tunnel list --output json | jq -r '.[0].name')
cloudflared tunnel route dns "$TUNNEL" ballastlane-tech.store
cloudflared tunnel route dns "$TUNNEL" auth.ballastlane-tech.store
cloudflared tunnel route dns "$TUNNEL" api.ballastlane-tech.store
```

Each command creates a CNAME in the `ballastlane-tech.store` zone pointing at the
tunnel's `<uuid>.cfargotunnel.com` endpoint. The first command needs the apex
record — Cloudflare allows it natively (CNAME flattening) without any extra
setup.

Edit `/home/ubuntu/.cloudflared/config.yml` and merge the ingress rules from
[`deploy/cloudflared-config.example.yml`](cloudflared-config.example.yml) above
the existing `service: http_status:404` catch-all. Restart the tunnel:

```bash
sudo systemctl restart cloudflared-tunnel
sudo systemctl status cloudflared-tunnel --no-pager | head
```

### 5. First deploy

```bash
bash /home/ubuntu/ballastlane-tech-store/deploy/deploy.sh
```

Expected output:

```
==> git pull
==> Loading .env
==> Building images
==> Bringing the stack up
==> Waiting for APIs to report healthy
    OK http://127.0.0.1:5101/health
    OK http://127.0.0.1:5102/health
    OK http://127.0.0.1:5174/
==> Container status
NAME                              STATUS
ballastlanetechstore-postgres     Up (healthy)
ballastlanetechstore-auth         Up
ballastlanetechstore-store        Up
ballastlanetechstore-web          Up
==> Deploy complete.
```

Verify externally from your laptop:

```bash
curl -I https://ballastlane-tech.store
curl -s https://auth.ballastlane-tech.store/health
curl -s https://api.ballastlane-tech.store/health
```

## Subsequent deploys

After pushing to `main`:

```bash
ssh -i ssh-key-2026-04-03.key ubuntu@168.138.155.229 \
  'sudo -E bash /home/ubuntu/ballastlane-tech-store/deploy/deploy.sh'
```

`deploy.sh` is idempotent — runs `git reset --hard origin/main`, rebuilds, and
restarts. ~60 s end-to-end on this VM. The `sudo -E` preserves the env vars
loaded from `.env` (otherwise the prod compose `${JWT_SIGNING_KEY:?…}` guards
fire under sudo's clean environment).

## Rollback

The Postgres data volume is named `ballastlane-tech-store_postgres_data` and
survives `docker compose down`. To roll back the application code while keeping
the data:

```bash
cd /home/ubuntu/ballastlane-tech-store
git reset --hard <previous-good-sha>
bash deploy/deploy.sh
```

## Operations

| Action | Command |
|---|---|
| Tail all logs | `cd /home/ubuntu/ballastlane-tech-store && docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f` |
| Restart one service | `docker compose -f docker-compose.yml -f docker-compose.prod.yml restart auth-api` |
| Wipe + reseed DB | `docker compose -f docker-compose.yml -f docker-compose.prod.yml down -v && bash deploy/deploy.sh` |
| Container stats | `docker stats --no-stream` |

## Troubleshooting

- **`bootstrap.sh` fails on `git clone` (Authentication failed)** — repo is
  private. Either flip the repo to public (`gh repo edit … --visibility public`)
  or use the SSH deploy-key fallback in step 2.
- **`apt` is locked by `apt.systemd.daily`** — periodic Ubuntu timer can wedge.
  Stop both timers, kill the stuck `apt-get`, then retry the script:
  ```bash
  sudo systemctl stop apt-daily.timer apt-daily-upgrade.timer
  sudo pkill -9 -f apt-get
  ```
- **`deploy.sh` fails with `failed to bind host port 127.0.0.1:5101`** — the
  prod compose overlay needs `ports: !override` to replace the base port list,
  not merge with it. Already applied; if you see this on a future change, check
  the merged config with `docker compose -f … config | grep ports -A4`.
- **`deploy.sh` health-check times out on `:5174`** — check
  `docker compose logs web`. Most common: build args were missing, so the bundle
  baked in `http://localhost:5101` and CORS rejects it from the public origin.
  Re-export the `.env` and rebuild with `--no-cache`.
- **One API crashes on cold start with `pg_type_typname_nsp_index` duplicate
  key** — fixed in `MigrationRunner.cs` via a Postgres advisory lock that
  serialises the two API processes' migration runs. If it recurs, confirm the
  advisory lock is still in place.
- **Cloudflare hostname returns 502** — tunnel ingress is pointing at a port no
  container is bound to. Confirm `ss -tlnp | grep 127.0.0.1` shows 5101, 5102,
  5174. Check `journalctl -u cloudflared-tunnel --no-pager -n 30 | grep -iE
  '502|error'` for the exact upstream error.
- **`https://<host>` returns SSL handshake errors right after zone activation**
  — Cloudflare's universal SSL cert hasn't been issued yet. Takes 1–15 min after
  the zone goes Active. Just wait; nothing on our side to fix.
- **JWT 401s after deploy** — `JWT_SIGNING_KEY` changed between deploys.
  Existing demo tokens in browser localStorage are invalidated. Sign in again.

## Resource footprint

Tech-store full stack on this VM consumes roughly:

| Service | Memory | CPU (idle) |
|---|---|---|
| postgres | ~80 MB | < 1% |
| auth-api | ~120 MB | < 1% |
| store-api | ~140 MB | < 1% |
| web (nginx) | ~10 MB | < 1% |

Total ~350 MB, comfortably below the VM's 24 GB. Co-tenancy with linkedin-monitor
(~600 MB) leaves >22 GB free.
