#!/usr/bin/env bash
# Downloads every file the voice eval needs into <cache>, checking each one's SHA-256: the phone's model files from
# the exporter's out/manifest.json, stored as <cache>/<model>/<fileName> (the layout the phone uses), and the eval's
# own tools from eval-models.manifest.
# Usage: tools/voice-eval/fetch-models.sh <cache> [manifest.json] [eval-models.manifest]
# Safe to rerun, and to run twice at once: a lock serialises callers; a file already checked is skipped; a download
# goes to <file>.part, resumes from there, and is renamed into place only once its SHA-256 matches.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
cache="${1:?usage: fetch-models.sh <cache> [manifest.json] [eval-models.manifest]}"
models_json="${2:-$here/out/manifest.json}"
eval_manifest="${3:-$here/eval-models.manifest}"

[ -f "$models_json" ] || { echo "no $models_json: run the exporter first (see README.md)" >&2; exit 1; }
[ -f "$eval_manifest" ] || { echo "no $eval_manifest" >&2; exit 1; }

# Every file as "<path in the cache> <url> <sha256>".
entries() {
  node -e '
    const files = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
    for (const f of files) console.log(`${f.model}/${f.fileName} ${f.url} ${f.sha256}`);
  ' "$models_json"
  local line
  while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in ''|'#'*) continue ;; esac
    echo "$line"
  done <"$eval_manifest"
}

list="$(entries)"

# Refuse a malformed list before anything is downloaded.
while read -r path url sha; do
  case "$sha" in *[!0-9a-f]*|'') echo "no 64-hex SHA-256 for $path" >&2; exit 1 ;; esac
  [ "${#sha}" -eq 64 ] || { echo "no 64-hex SHA-256 for $path" >&2; exit 1; }
  case "/$path/" in */../*|//*) echo "path $path leaves the cache" >&2; exit 1 ;; esac
  [ -n "$url" ] || { echo "no url for $path" >&2; exit 1; }
done <<<"$list"

mkdir -p "$cache"
cache="$(cd "$cache" && pwd)"
exec 9>"$cache/.fetch.lock"
flock 9

sha_of() { sha256sum "$1" | cut -d' ' -f1; }

unpack() { # <archive>: into <archive without the extension>/, once
  local archive="$1" into tmp
  into="${archive%.tar.*}"
  [ -d "$into" ] && return 0
  rm -rf "$into".tmp.*
  tmp="$(mktemp -d "$into.tmp.XXXXXX")"
  tar -xf "$archive" -C "$tmp"
  mv "$tmp" "$into"
  echo "unpacked into $into"
}

fetch() { # <path> <url> <sha>
  local path="$1" url="$2" sha="$3" target part got
  target="$cache/$path"
  part="$target.part"
  mkdir -p "$(dirname "$target")"
  if [ -f "$target" ] && [ "$(sha_of "$target")" = "$sha" ]; then
    echo "already checked: $path"
  else
    rm -f "$target"
    if ! { [ -f "$part" ] && [ "$(sha_of "$part")" = "$sha" ]; }; then
      echo "downloading $path"
      curl -fL --retry 3 -C - -o "$part" "$url" || {
        echo "download of $path stopped; rerun to resume from $part (or delete it to start again)" >&2
        exit 1
      }
    fi
    got="$(sha_of "$part")"
    if [ "$got" != "$sha" ]; then
      rm -f "$part"
      echo "SHA-256 mismatch for $path: expected $sha, got $got" >&2
      exit 1
    fi
    mv "$part" "$target"
    echo "checked: $path"
  fi
  case "$path" in *.tar.gz|*.tar.bz2) unpack "$target" ;; esac
}

while read -r path url sha; do
  fetch "$path" "$url" "$sha"
done <<<"$list"
echo "every file is in $cache"
