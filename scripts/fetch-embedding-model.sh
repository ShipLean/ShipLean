#!/usr/bin/env bash
# Downloads the pinned embedding model into <dir>, checking every file's SHA-256.
# Usage: scripts/fetch-embedding-model.sh <dir> [manifest]   (manifest default: embedding-model.manifest beside this script)
# Safe to run twice or at the same time: a lock serialises callers, files are fetched into a temp dir beside <dir>
# and renamed into place, so <dir> is either complete and verified or absent. A verified <dir> is left alone.
set -euo pipefail

dir="${1:?usage: fetch-embedding-model.sh <dir> [manifest]}"
here="$(cd "$(dirname "$0")" && pwd)"
manifest="${2:-$here/embedding-model.manifest}"
manifest_dir="$(cd "$(dirname "$manifest")" && pwd)"

dir="${dir%/}"
mkdir -p "$(dirname "$dir")"
parent="$(cd "$(dirname "$dir")" && pwd)"
dir="$parent/$(basename "$dir")"

# Calls "$1 name url sha" for each manifest line.
each_entry() {
  local line name url sha
  while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in ''|'#'*) continue ;; esac
    read -r name url sha <<<"$line"
    "$1" "$name" "$url" "$sha"
  done <"$manifest"
}

verified() { # <dir> <name> <sha>
  [ -f "$1/$2" ] && [ "$(sha256sum "$1/$2" | cut -d' ' -f1)" = "$3" ]
}

all_present() {
  local ok=0
  check() { verified "$dir" "$1" "$3" || ok=1; }
  each_entry check
  return $ok
}

# Refuse a malformed manifest before anything is downloaded.
bad_sha() { case "$3" in *[!0-9a-f]*|'') return 0 ;; esac; [ "${#3}" -ne 64 ]; }
check_sha() { if bad_sha "$@"; then echo "manifest line for $1 has no 64-hex SHA-256" >&2; exit 1; fi; }
each_entry check_sha

exec 9>"$dir.lock"
flock 9

# Leftovers of killed runs (we hold the lock, so none is in use).
rm -rf "$dir".tmp.*

if [ -d "$dir" ] && all_present; then
  echo "embedding model already in $dir"
  exit 0
fi

tmp="$(mktemp -d "$dir.tmp.XXXXXX")"
trap 'rm -rf "$tmp"' EXIT
chmod 755 "$tmp"

fetch() { # name url sha
  local name="$1" url="$2" sha="$3"
  case "$url" in
    local:*) cp "$manifest_dir/${url#local:}" "$tmp/$name" ;;
    *) curl -fsSL --retry 3 -o "$tmp/$name" "$url" ;;
  esac
  verified "$tmp" "$name" "$sha" || { echo "SHA-256 mismatch for $name (expected $sha)" >&2; exit 1; }
}
each_entry fetch

# Swap in: the old directory (if any, and stale) goes aside first, so a crash leaves a whole directory or none.
if [ -d "$dir" ]; then mv "$dir" "$tmp.old"; fi
mv "$tmp" "$dir"
trap - EXIT
rm -rf "$tmp.old"
echo "embedding model fetched into $dir"
