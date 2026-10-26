#!/usr/bin/env bash
# Regenerate (or verify) the page-screenshot baselines inside the CI image, the only place they are comparable.
#
#   scripts/update-page-shots.sh [spec…]            regenerate: --update-snapshots=all, then fix file ownership
#   scripts/update-page-shots.sh --verify [spec…]   compare only, three repeats each (the 3× stability proof)
#
# A spec is a path under src/ShipLean.Client (src/pages/screenshots/account.spec.tsx) or a bare slice name (account).
# No spec runs every page-screenshot spec. Safe to run from several worktrees at once: each run is its own throwaway
# container with its own node_modules; only the pnpm store (a content-addressed, lock-guarded cache) is shared, in the
# named volume shiplean-pnpm-store. Env: PAGE_SHOTS_WORKERS (default 2).
set -euo pipefail

IMAGE=mcr.microsoft.com/playwright:v1.62.0-resolute
STORE_VOLUME=shiplean-pnpm-store

client_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
repo_root=$(cd "$client_dir/../.." && pwd)
pnpm_version=$(sed -n 's/.*"packageManager": "pnpm@\([^"]*\)".*/\1/p' "$client_dir/package.json")
[ -n "$pnpm_version" ] || { echo "no packageManager pnpm@… in package.json" >&2; exit 1; }

mode=update
specs=()
for arg in "$@"; do
  case "$arg" in
    --verify) mode=verify ;;
    -*) echo "unknown option $arg" >&2; exit 2 ;;
    */*) specs+=("$arg") ;;
    *) specs+=("src/pages/screenshots/${arg%.spec.tsx}.spec.tsx") ;;
  esac
done
[ ${#specs[@]} -gt 0 ] || specs=(src/pages/screenshots)

if [ "$mode" = verify ]; then
  pw_args="--repeat-each=3"
else
  pw_args="--update-snapshots=all"
fi

docker volume create "$STORE_VOLUME" >/dev/null
rel_client=${client_dir#"$repo_root"/}

docker run --rm \
  -e CI=true \
  -e PNPM_VERSION="$pnpm_version" \
  -e PW_ARGS="$pw_args --workers=${PAGE_SHOTS_WORKERS:-2}" \
  -e HOST_UID="$(id -u)" -e HOST_GID="$(id -g)" \
  -v "$repo_root":/w \
  -v /w/"$rel_client"/node_modules \
  -v "$STORE_VOLUME":/pnpm-store \
  -w /w/"$rel_client" \
  "$IMAGE" bash -c '
    status=0
    {
      npm install -g --silent "pnpm@$PNPM_VERSION" &&
      pnpm config set store-dir /pnpm-store --location=global &&
      pnpm install --frozen-lockfile --prefer-offline &&
      pnpm exec playwright test -c playwright-ct.config.ts "$@" $PW_ARGS
    } || status=$?
    chown -R "$HOST_UID:$HOST_GID" playwright/.cache test-results playwright-report src 2>/dev/null || true
    exit $status
  ' bash "${specs[@]}"
