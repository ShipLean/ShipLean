#!/usr/bin/env bash
# Regenerates images/<name>.<theme>.png: the touch shell (SHIP-713) and the work item page (SHIP-714, item-* and
# feature-*) on the reference screens at 390 × 844, 2× scale.
# Usage: ./screenshots.sh [name ...]   (no names = all). CHROME=/path/to/chrome overrides the browser.
# The today-*.png images are screenshots of the real app (main at 918cf067) and are not made here.
# Item shots use ?at=<section>, which with ?shot hides what is above the section: --screenshot ignores scrolling.
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
  # The work item page (SHIP-714)
  [item]="work-item.html"
  [item-state]="work-item.html?dirty&open=state"
  [item-parent]="work-item.html?open=parent"
  [item-tasks]="work-item.html?dirty&at=Tasks"
  [item-discussion]="work-item.html?at=Discussion&mention"
  [item-rail]="work-item.html?at=Links"
  [item-conflict]="work-item.html?conflict&at=Conflict"
  [item-bug]="work-item.html?key=SHIP-153&at=Repro"
  [item-action]="work-item.html?key=SHIP-188"
  [item-modal]="work-item.html?peek&at=Tasks"
  [feature]="feature.html"
  [feature-status-edit]="feature.html?band=edit"
  [feature-description]="feature.html?at=Description"
  [feature-links]="feature.html?at=Links"
  [feature-children]="feature.html?at=Stories%20and%20bugs"
  [feature-log]="feature.html?at=Log"
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
