# Touch shell (SHIP-713)

The frame every screen sits in, at 900px wide and below: one sticky top bar, a navigation drawer, the project picker as a bottom sheet and quick search taking the whole screen. Above 900px nothing changes. The rules are in [design/README.md › Touch shell](../README.md#touch-shell-ship-713); the page stories under SHIP-707 (SHIP-714 to SHIP-737) build on them.

| File | What it is |
|---|---|
| `index.html` | The design review: today on a phone, every state, the top bar part by part, the rules for every page, drawer against bottom tabs, how to measure it, and the decisions. Open it directly in a browser. |
| `images/<state>.<theme>.png` | The touch shell on the reference screens at 390 × 844, 2× scale, light and dark. |
| `images/today-*.png` | The real app on a phone before this change (main at 918cf067, seeded local instance). |
| `screenshots.sh` | Regenerates `images/<state>.<theme>.png` from `../screens/` with headless Chrome. |

The shell itself is built in `../screens/shared.js` and `../screens/shiplean.css`, so any reference screen shows it in a window narrower than 900px. These address parameters open its states: `?nav` (drawer), `?picker`, `?search` or `?search=pay`, `?usermenu`, `?offline` (Reconnecting).

## Today

| Stories backlog | Project picker | Quick search |
|---|---|---|
| ![Today: the Stories backlog on a phone, the menu strip wrapping to three rows](images/today-backlog.png) | ![Today: the desktop project picker popover on a phone](images/today-picker.png) | ![Today: quick search on a phone](images/today-search.png) |

1. 165px of frame before any content: the menu strip wraps to three rows and the breadcrumb bar adds 38px, and all of it scrolls away.
2. Epics, Features, the current sprint and the retro pages can't be reached: sub-items are hidden under 900px.
3. Text fields under 16px make iOS Safari zoom the page.
4. 30px rows, key caps and keyboard hints on a touch screen.
5. The shell switches at 900px and quick search at 899px.
6. No safe areas for the notch and the home indicator.

## The touch shell

| State | Light | Dark |
|---|---|---|
| Closed (Stories backlog) | ![Touch shell closed, light](images/shell.light.png) | ![Touch shell closed, dark](images/shell.dark.png) |
| Drawer | ![Drawer, light](images/drawer.light.png) | ![Drawer, dark](images/drawer.dark.png) |
| Drawer from the Taskboard | ![Drawer with Sprints active, light](images/drawer-sprints.light.png) | ![Drawer with Sprints active, dark](images/drawer-sprints.dark.png) |
| Switch project | ![Project picker bottom sheet, light](images/picker.light.png) | ![Project picker bottom sheet, dark](images/picker.dark.png) |
| Quick search | ![Quick search full screen, light](images/search.light.png) | ![Quick search full screen, dark](images/search.dark.png) |
| Quick search, “pay” | ![Quick search results, light](images/search-results.light.png) | ![Quick search results, dark](images/search-results.dark.png) |
| User menu | ![User menu in the drawer, light](images/user-menu.light.png) | ![User menu in the drawer, dark](images/user-menu.dark.png) |
| Sprint tabs | ![Sprint backlog with its tabs, light](images/sprint-tabs.light.png) | ![Sprint backlog with its tabs, dark](images/sprint-tabs.dark.png) |
| Reconnecting | ![Top bar while reconnecting, light](images/reconnecting.light.png) | ![Top bar while reconnecting, dark](images/reconnecting.dark.png) |
| A page with no project | ![Your profile, light](images/profile.light.png) | ![Your profile, dark](images/profile.dark.png) |

The pages inside the frame are still their desktop layouts; their phone versions are the page stories.
