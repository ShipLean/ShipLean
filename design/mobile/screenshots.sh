#!/usr/bin/env bash
# Regenerates images/<name>.<theme>.png: the touch shell (SHIP-713) and the work item page (SHIP-714, item-* and
# feature-*) on the reference screens at 390 × 844, 2× scale, and the page stories' phone screens kept in this folder
# (an address starting ./, such as SHIP-715's attachment.html).
# Usage: ./screenshots.sh [name ...]   (no names = all). CHROME=/path/to/chrome overrides the browser.
# The today-*.png images are screenshots of the real app (main at 918cf067) and are not made here.
# (today-attachment-*.png and today-fboard*.png come from the SPA's component harness at 390 × 844 with the page-shot
# fixtures.)
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
  # Markdown attachments and images (SHIP-715)
  [attachment]="./attachment.html"
  [attachment-scrolled]="./attachment.html?state=scrolled"
  [attachment-contents]="./attachment.html?state=contents"
  [attachment-about]="./attachment.html?state=about"
  [attachment-files]="./attachment.html?state=files"
  [attachment-menu]="./attachment.html?state=menu"
  [attachment-source]="./attachment.html?state=source"
  [attachment-edit]="./attachment.html?state=edit"
  [attachment-viewer]="./attachment.html?state=viewer"
  [attachment-viewer-zoom]="./attachment.html?state=viewer-zoom"
  [attachment-toolarge]="./attachment.html?state=toolarge"
  # The Feature board (SHIP-722)
  [fboard]="feature-board.html"
  [fboard-active]="feature-board.html?col=active"
  [fboard-drag]="feature-board.html?drag=SHIP-176&to=active"
  [fboard-task]="feature-board.html?sheet=task&task=SHIP-172&col=active"
  [fboard-story]="feature-board.html?sheet=story&task=SHIP-172"
  [fboard-assign]="feature-board.html?sheet=assign&task=SHIP-176"
  [fboard-filter]="feature-board.html?filter=mei&sheet=filter"
  [fboard-filtered]="feature-board.html?filter=mei"
  [fboard-end]="feature-board.html?at=SHIP-160"
  [fboard-closed]="feature-board.html?closed&at=SHIP-160"
  [fboard-more]="feature-board.html?sheet=more"
  [fboard-add]="feature-board.html?add"
  [fboard-parent]="feature-board.html?key=SHIP-103"
  [fboard-parent-section]="feature-board.html?key=SHIP-103&at=SHIP-108"
  [fboard-people]="feature-board.html?key=SHIP-103&view=people"
)

names=("$@")
[[ ${#names[@]} -gt 0 ]] || names=("${!shots[@]}")

mkdir -p images
for name in "${names[@]}"; do
  url="${shots[$name]}"
  base="$screens"
  if [[ "$url" == ./* ]]; then base="$PWD"; url="${url#./}"; fi
  sep='?'; [[ "$url" == *\?* ]] && sep='&'
  for theme in light dark; do
    "$chrome" --headless=new --no-sandbox --hide-scrollbars --force-device-scale-factor=2 \
      --window-size=390,844 --virtual-time-budget=4000 \
      --screenshot="$PWD/images/$name.$theme.png" "file://$base/$url${sep}shot&theme=$theme" >/dev/null 2>&1
    echo "wrote mobile/images/$name.$theme.png"
  done
done
