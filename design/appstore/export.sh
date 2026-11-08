#!/usr/bin/env bash
# Writes every store asset (SHIP-930) to out/<file> at its exact pixel size, then checks size and alpha.
# Usage: ./export.sh [id-prefix ...]   (no args = all). CHROME=/path/to/chrome overrides the browser.
# On the shared host: CHROME=~/.cache/ms-playwright/chromium_headless_shell-1234/*/chrome-headless-shell
#                     LD_LIBRARY_PATH=~/.local/pwlibs/usr/lib/x86_64-linux-gnu
set -euo pipefail
cd "$(dirname "$0")"
chrome="${CHROME:-}"
if [[ -z "$chrome" ]]; then
  for c in google-chrome chromium chromium-browser chrome-headless-shell; do
    if command -v "$c" >/dev/null; then chrome="$c"; break; fi
  done
fi
[[ -n "$chrome" ]] || { echo "No Chrome found; set CHROME=/path/to/chrome" >&2; exit 1; }
here="file://$PWD"
base=(--headless --disable-gpu --no-sandbox --hide-scrollbars --force-device-scale-factor=1 --virtual-time-budget=8000)
list=$("$chrome" "${base[@]}" --window-size=800,600 --dump-dom "$here/asset.html?list" 2>/dev/null | grep -o 'APPASSETS:.*:END' | sed 's/^APPASSETS://; s/:END$//')
python3 -I - "$list" "$@" > out.tsv <<'PY'
import json, sys
assets = json.loads(sys.argv[1]); prefixes = sys.argv[2:]
for a in assets:
    if prefixes and not any(a["id"].startswith(p) for p in prefixes): continue
    print("\t".join([a["id"], str(a["w"]), str(a["h"]), a["file"], "1" if a["alpha"] else "0"]))
PY
while IFS=$'\t' read -r id w h file alpha; do
  mkdir -p "out/$(dirname "$file")"
  extra=(); [[ "$alpha" == 1 ]] && extra=(--default-background-color=00000000)
  "$chrome" "${base[@]}" "${extra[@]}" --window-size="$w,$h" --screenshot="out/$file" "$here/asset.html?id=$id" >/dev/null 2>&1
  # Chrome writes a fully opaque page as RGB even on a clear background; the alpha assets must be RGBA.
  [[ "$alpha" == 1 ]] && python3 -I ./to-rgba.py "out/$file"
  echo "out/$file"
done < out.tsv
python3 -I ./check-png.py out.tsv
rm -f out.tsv
