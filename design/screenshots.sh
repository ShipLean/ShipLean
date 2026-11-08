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
  [backlog-selected]="backlog.html?state=selected"
  [backlog-paste]="backlog.html?state=paste"
  [backlog-modal]="backlog.html?state=modal"
  [backlog-add]="backlog.html?state=add"
  [backlog-queue]="backlog.html?state=queue"
  [backlog-keys]="backlog.html?state=keys"
  [planning-selected]="planning.html?state=selected"
  [planning-drag]="planning.html?state=drag"
  [planning-over]="planning.html?pull=6"
  [planning-row-changed]="planning.html?state=conflict"
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
  [sprint-retro-items]="sprint-retro.html?view=items"
  [sprint-retro-decide]="sprint-retro.html?decide=open"
  [retro-item-editing]="retro-item.html?key=SHIP-104&adding=SHIP-104:bad&why=R-312.4"
  [retro-item-new-action]="retro-item.html?dialog=action"
  [retro-insights-weeks]="retro-insights.html?range=w6"
  [retro-insights-status-tip]="retro-insights.html?tip=status"
  [feature-retros]="feature.html?retros"
  [work-item-action]="work-item.html?key=SHIP-188"
  [retros-search]="retros.html?q=screenshot"
  [retro-insights-actions]="retro-insights.html?view=actions"
  [retro-insights-impact]="retro-insights.html?view=impact"
  [sprint-demo-closable]="sprint-demo.html?state=closable"
  [sprint-demo-none]="sprint-demo.html?state=none"
  [sprint-demo-blocked]="sprint-demo.html?state=blocked"
  [sprint-demo-working]="sprint-demo.html?state=working"
  [sprint-demo-failed]="sprint-demo.html?state=failed"
  [sprint-demo-youtube-unlisted]="sprint-demo.html?state=youtube-unlisted"
  [sprint-demo-youtube-private]="sprint-demo.html?state=youtube-private"
  [sprint-demo-youtube-failed]="sprint-demo.html?state=youtube-failed"
  [settings-demos]="settings.html?tab=demos"
  [settings-demos-logo]="settings.html?tab=demos&brand=logo"
  [settings-demos-logo-refused]="settings.html?tab=demos&brand=refused"
  [settings-demos-tokens-refused]="settings.html?tab=demos&brand=tokens-refused"
  [settings-demos-yt-off]="settings.html?tab=demos&yt=off"
  [settings-demos-yt-connect]="settings.html?tab=demos&yt=connect"
  [settings-demos-yt-pending]="settings.html?tab=demos&yt=pending"
  [settings-demos-yt-refused]="settings.html?tab=demos&yt=refused"
  [settings-demos-yt-connected]="settings.html?tab=demos&yt=connected"
  [instance-integrations]="instance.html?tab=integrations"
  # Decisions and questions on the work item page (SHIP-922)
  [decisions-change]="decisions.html?state=change"
  [decisions-settled-change]="decisions.html?state=settled-change"
  [decisions-history]="decisions.html?state=history"
  [decisions-reply]="decisions.html?state=reply"
  [decisions-folded]="decisions.html?state=folded"
  [decisions-empty]="decisions.html?state=empty"
  [decisions-feature]="decisions.html?state=feature"
  # SHIP-907: Health on Features (S5) and Your profile › Signed-in devices
  [feature-health-menu]="feature.html?menu=health"
  [feature-health-none]="feature.html?health=none"
  [features-board-compact]="features-board.html?compact"
  [features-board-health-filter]="features-board.html?fhealth=risk"
  [profile-devices]="profile.html?at=sessions"
  [profile-devices-signout]="profile.html?at=sessions&devout=px"
  [profile-devices-none]="profile.html?at=sessions&devices=0"
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
