#!/usr/bin/env bash
# Regenerates images/<name>.<theme>.png: the touch shell (SHIP-713), the work item page (SHIP-714, item-* and
# feature-*) and the boards (SHIP-719: board-*, features-board-*, epics-board-*) on the reference screens at 390 × 844,
# 2× scale, and the page stories' phone screens kept in this folder
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
  # Backlogs: stories, features and epics (SHIP-716)
  [backlogs]="./backlogs.html"
  [backlogs-more]="./backlogs.html?state=more"
  [backlogs-search]="./backlogs.html?state=search"
  [backlogs-add]="./backlogs.html?state=add"
  [backlogs-filter]="./backlogs.html?state=filter"
  [backlogs-row]="./backlogs.html?state=row"
  [backlogs-drag]="./backlogs.html?state=drag"
  [backlogs-select]="./backlogs.html?state=select"
  [backlogs-bulk-sprint]="./backlogs.html?state=bulk-sprint"
  [backlogs-points]="./backlogs.html?state=points"
  [backlogs-planning]="./backlogs.html?mode=planning"
  [backlogs-planning-drag]="./backlogs.html?mode=planning&state=drag"
  [backlogs-planning-more]="./backlogs.html?mode=planning&state=more"
  [backlogs-planning-section]="./backlogs.html?mode=planning&state=section"
  [backlogs-tree]="./backlogs.html?mode=tree"
  [backlogs-features]="./backlogs.html?level=features"
  [backlogs-epics]="./backlogs.html?level=epics"
  # Sign in on a phone (SHIP-736)
  [sign-in]="./sign-in.html"
  [sign-in-keyboard]="./sign-in.html?kb"
  [sign-in-password-only]="./sign-in.html?google=0"
  [sign-in-error]="./sign-in.html?state=error"
  [sign-in-signed-out]="./sign-in.html?state=signed-out"
  [sign-in-ended]="./sign-in.html?state=ended"
  [sign-in-google-failed]="./sign-in.html?state=google-failed"
  [sign-in-google-refused]="./sign-in.html?state=google-refused"
  [sign-in-change]="./sign-in.html?state=change"
  [sign-in-change-typed]="./sign-in.html?state=change&typed"
  [sign-in-no-projects]="./sign-in.html?state=no-projects"
  [sign-in-no-projects-member]="./sign-in.html?state=no-projects-member"
  [sign-in-proxy]="./sign-in.html?state=proxy"
  [sign-in-proxy-disabled]="./sign-in.html?state=proxy-disabled"
  [sign-in-proxy-ended]="./sign-in.html?state=proxy-ended"
  [sign-in-instance-shiplean]="./sign-in.html?inst=ShipLean"
  # Boards: stories, features and epics (SHIP-719). *-before is the same screen with the touch layer off (?notouch).
  [board-before]="board.html?notouch"
  [board]="board.html"
  [board-ready]="board.html?col=ready"
  [board-drag]="board.html?col=ready&drag=SHIP-147&to=prog"
  [board-card]="board.html?col=prog&sheet=card:SHIP-129"
  [board-filter]="board.html?sheet=filter&who=ana"
  [board-more]="board.html?sheet=more"
  [board-assign]="board.html?col=new&sheet=assign:SHIP-170"
  [board-dod]="board.html?col=ready&sheet=dod:ready"
  [board-compact]="board.html?compact&col=prog"
  [features-board-before]="features-board.html?notouch"
  [features-board]="features-board.html?col=prog"
  [features-board-children]="features-board.html?open=SHIP-101&col=prog"
  [features-board-lanes]="features-board.html?lanes=epic&col=shaping"
  [features-board-drag]="features-board.html?drag=SHIP-108&to=prog"
  [features-board-card]="features-board.html?col=release&sheet=card:SHIP-111"
  [features-board-done-check]="features-board.html?confirm=SHIP-102&col=release"
  [epics-board-before]="features-board.html?level=epics&notouch"
  [epics-board]="features-board.html?level=epics"
  [epics-board-card]="features-board.html?level=epics&col=prog&sheet=card:SHIP-90"
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
