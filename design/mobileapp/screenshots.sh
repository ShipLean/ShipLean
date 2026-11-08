#!/usr/bin/env bash
# Regenerates images/<page>-<state>.<theme>.png for every app screen (SHIP-905) at 390 × 844, 2× scale.
# Each *.html here (except index.html and files starting _) declares its states via App.screen; ?list prints them.
# Usage: ./screenshots.sh [page ...]   (no names = all pages). CHROME=/path/to/chrome overrides the browser.
set -euo pipefail
cd "$(dirname "$0")"
chrome="${CHROME:-}"
if [[ -z "$chrome" ]]; then
  for c in google-chrome chromium chromium-browser chrome-headless-shell; do
    if command -v "$c" >/dev/null; then chrome="$c"; break; fi
  done
fi
[[ -n "$chrome" ]] || { echo "No Chrome found; set CHROME=/path/to/chrome" >&2; exit 1; }
mkdir -p images
pages=("$@")
if [[ ${#pages[@]} -eq 0 ]]; then
  for f in *.html; do [[ "$f" == index.html || "$f" == _* ]] || pages+=("${f%.html}"); done
fi
here="file://$PWD"
flags=(--headless --disable-gpu --no-sandbox --hide-scrollbars --force-device-scale-factor=2 --window-size=390,844 --virtual-time-budget=4000)
for page in "${pages[@]}"; do
  states=$("$chrome" "${flags[@]}" --dump-dom "$here/$page.html?list" 2>/dev/null | grep -o 'APPSTATES:[^:]*:END' | head -1 | sed 's/^APPSTATES://; s/:END$//' | tr ',' ' ')
  for s in $states; do
    for theme in light dark; do
      "$chrome" "${flags[@]}" --screenshot="images/$page-$s.$theme.png" "$here/$page.html?state=$s&theme=$theme" >/dev/null 2>&1
      echo "images/$page-$s.$theme.png"
    done
  done
done
