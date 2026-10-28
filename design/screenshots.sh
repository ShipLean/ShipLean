#!/usr/bin/env bash
# Regenerate the reference screenshots: screens/<name>.<theme>.png at 1440×900, 2× scale.
# Usage: design/screenshots.sh [screen-name ...]   (default: every screens/*.html, plus the variants below)
# Needs a headless Chrome/Chromium. Set CHROME to its path if it isn't on PATH.
set -euo pipefail
cd "$(dirname "$0")/screens"

chrome="${CHROME:-}"
if [[ -z "$chrome" ]]; then
  for c in google-chrome chromium chromium-browser chrome-headless-shell; do
    if command -v "$c" >/dev/null; then chrome="$c"; break; fi
  done
fi
[[ -n "$chrome" ]] || { echo "No Chrome found; set CHROME=/path/to/chrome" >&2; exit 1; }

# Variants: another state of one screen, shot under its own name.
declare -A variants=(
  [taskboard-people]="taskboard.html?view=people"
  [taskboard-settings]="taskboard.html?dialog=settings"
  [taskboard-styles]="taskboard.html?dialog=settings&pane=styles"
  [backlog-tree]="backlog.html?tree"
  [features-epics]="features.html?level=epics"
  [features-board-lanes]="features-board.html?lanes=epic&open=SHIP-101"
  [epics-board]="features-board.html?level=epics&open=SHIP-90"
  [features-board-done-confirm]="features-board.html?confirm=SHIP-102"
  [work-item-bug]="work-item.html?key=SHIP-153"
  [import-key]="import.html?step=3"
  [import-run]="import.html?step=5&run=errors"
  [board-settings-styles]="board-settings.html?pane=styles"
  [signin-error]="signin.html?state=error"
  [signin-change]="signin.html?state=change"
  [signin-no-projects]="signin.html?state=no-projects"
  [instance-general]="instance.html?tab=general"
  [instance-projects]="instance.html?tab=projects"
  [profile-photo]="profile.html?photo"
  [user-menu]="backlog.html?usermenu"
  [feature-blocked]="feature.html?band=blocked"
  [feature-parked]="feature.html?band=parked"
  [feature-state-menu]="feature.html?menu=state"
  [feature-status-edit]="feature.html?band=edit"
  [feature-edit]="feature.html?edit"
  [feature-drop]="feature.html?drop"
  [feature-link-og]="feature.html?addlink=og"
  [feature-link-github]="feature.html?addlink=github"
  [feature-link-private]="feature.html?addlink=private"
  [attachment-source]="attachment.html?view=source"
  [features-new]="features.html?dialog=new"
  [feature-board-parent]="feature-board.html?key=SHIP-103"
  [feature-board-people]="feature-board.html?key=SHIP-103&view=people"
)

names=("$@")
if [[ ${#names[@]} -eq 0 ]]; then
  for f in *.html; do names+=("${f%.html}"); done
  names+=("${!variants[@]}")
fi

for name in "${names[@]}"; do
  url="${variants[$name]:-$name.html}"
  [[ "$url" == *\?* ]] && sep="&" || sep="?"
  for theme in light dark; do
    out="$PWD/$name.$theme.png"
    "$chrome" --headless=new --no-sandbox --hide-scrollbars --force-device-scale-factor=2 \
      --window-size=1440,900 --virtual-time-budget=4000 \
      --screenshot="$out" "file://$PWD/$url${sep}shot&theme=$theme" >/dev/null 2>&1
    echo "wrote screens/$name.$theme.png"
  done
done
