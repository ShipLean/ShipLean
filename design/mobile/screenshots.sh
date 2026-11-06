#!/usr/bin/env bash
# Regenerates images/<name>.<theme>.png: the touch shell (SHIP-713) on the reference screens at 390 × 844, 2× scale.
# Usage: ./screenshots.sh [name ...]   (no names = all). CHROME=/path/to/chrome overrides the browser.
# The today-*.png images are screenshots of the real app (main at 918cf067) and are not made here.
set -euo pipefail
cd "$(dirname "$0")"
screens="$(cd ../screens && pwd)"

chrome="${CHROME:-}"
if [[ -z "$chrome" ]]; then
  for c in google-chrome chromium chromium-browser chrome-headless-shell; do
    if command -v "$c" >/dev/null; then chrome="$c"; break; fi
  done
fi
[[ -n "$chrome" ]] || { echo "No Chrome found; set CHROME=/path/to/chrome" >&2; exit 1; }

declare -A shots=(
  [shell]="backlog.html"
  [drawer]="backlog.html?nav"
  [drawer-sprints]="taskboard.html?nav"
  [picker]="backlog.html?picker"
  [search]="backlog.html?search"
  [search-results]="backlog.html?search=pay"
  [user-menu]="backlog.html?usermenu"
  [sprint-tabs]="sprint-backlog.html"
  [reconnecting]="taskboard.html?offline"
  [profile]="profile.html"
)

names=("$@")
[[ ${#names[@]} -gt 0 ]] || names=("${!shots[@]}")

mkdir -p images
for name in "${names[@]}"; do
  url="${shots[$name]}"
  sep='?'; [[ "$url" == *\?* ]] && sep='&'
  for theme in light dark; do
    "$chrome" --headless=new --no-sandbox --hide-scrollbars --force-device-scale-factor=2 \
      --window-size=390,844 --virtual-time-budget=4000 \
      --screenshot="$PWD/images/$name.$theme.png" "file://$screens/$url${sep}shot&theme=$theme" >/dev/null 2>&1
    echo "wrote mobile/images/$name.$theme.png"
  done
done
