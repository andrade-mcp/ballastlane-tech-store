# Deploy — Oracle Cloud (consultinfra2 / instance-20260402-2351)

Production runbook for deploying tech-store to the Oracle ARM VM. Designed to
co-tenant cleanly with the existing `linkedin-monitor` service on the same VM.

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

The tunnel can only route hostnames in zones owned by the same Cloudflare account.
The domain is currently at the registrar — bring it under Cloudflare first.

1. Cloudflare dashboard → **Add a site** → enter `ballastlane-tech.store` → choose
   the **Free** plan.
2. Cloudflare scans existing DNS records (likely empty for a new domain) — accept
   them.
3. Cloudflare shows two **assigned nameservers** (e.g. `kate.ns.cloudflare.com`,
   `walt.ns.cloudflare.com`).
4. At the registrar (where `ballastlane-tech.store` was bought), replace the
   existing nameservers with the two Cloudflare ones.
5. Propagation: usually < 15 minutes for a new domain. Check with:
   ```bash
   dig +short NS ballastlane-tech.store
   ```
   Should return the two `*.ns.cloudflare.com` names.
6. In the Cloudflare zone settings, set **SSL/TLS → Overview → Full** (not
   Flexible). The tunnel handles the back-half of TLS internally.

Once the zone shows **Active** in Cloudflare, continue.

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
  'bash /home/ubuntu/ballastlane-tech-store/deploy/deploy.sh'
```

`deploy.sh` is idempotent — runs `git reset --hard origin/main`, rebuilds, and
restarts. ~60 s end-to-end on this VM.

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
  private. Use the SSH deploy-key fallback in step 2.
- **`deploy.sh` health-check times out on `:5174`** — check
  `docker compose logs web`. Most common: build args were missing, so the bundle
  baked in `http://localhost:5101` and CORS rejects it from the public origin.
  Re-export the `.env` and rebuild with `--no-cache`.
- **Cloudflare hostname returns 502** — tunnel ingress is pointing at a port no
  container is bound to. Confirm `ss -tlnp | grep 127.0.0.1` shows 5101, 5102,
  5174.
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
