#!/usr/bin/env bash
# One-time VM bootstrap for tech-store on Ubuntu 22.04 ARM64.
# Run this on the Oracle VM as the `ubuntu` user. Idempotent — safe to re-run.

set -euo pipefail

APP_DIR="${APP_DIR:-/home/ubuntu/ballastlane-tech-store}"
REPO_URL="${REPO_URL:-https://github.com/andrade-mcp/ballastlane-tech-store.git}"

echo "==> Updating apt index"
sudo apt-get update -y

echo "==> Installing Docker if not present"
if ! command -v docker >/dev/null 2>&1; then
    sudo apt-get install -y ca-certificates curl
    sudo install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | \
        sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    sudo chmod a+r /etc/apt/keyrings/docker.gpg
    echo \
        "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
        https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | \
        sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
    sudo apt-get update -y
    sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    sudo usermod -aG docker ubuntu
    echo "==> Docker installed. Log out and back in for group change to take effect."
fi

echo "==> Cloning or updating the repo at ${APP_DIR}"
if [ ! -d "${APP_DIR}/.git" ]; then
    git clone "${REPO_URL}" "${APP_DIR}"
else
    git -C "${APP_DIR}" fetch --all --prune
    git -C "${APP_DIR}" reset --hard origin/main
fi

echo "==> Checking for .env"
if [ ! -f "${APP_DIR}/.env" ]; then
    cp "${APP_DIR}/deploy/.env.production.example" "${APP_DIR}/.env"
    echo "    A skeleton .env was copied. Edit ${APP_DIR}/.env and fill in"
    echo "    JWT_SIGNING_KEY + PUBLIC_*_ORIGIN before running deploy.sh."
fi

echo "==> Granting passwordless sudo for compose restarts (one-shot)"
SUDOERS=/etc/sudoers.d/ballastlane-tech-store
if ! sudo test -f "${SUDOERS}"; then
    sudo tee "${SUDOERS}" >/dev/null <<EOF
ubuntu ALL=(ALL) NOPASSWD: /usr/bin/docker, /usr/bin/docker compose
EOF
    sudo chmod 0440 "${SUDOERS}"
fi

echo "==> Bootstrap complete."
echo "    Next: edit ${APP_DIR}/.env, then run ${APP_DIR}/deploy/deploy.sh"
