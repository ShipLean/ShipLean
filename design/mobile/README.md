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

## Markdown attachments and images (SHIP-715)

The Markdown attachment page and the image viewer at 900px and below, inside the touch shell. Desktop is unchanged.

| File | What it is |
|---|---|
| `attachment.html` | The phone screen, opened directly. `?state=rendered\|scrolled\|contents\|about\|files\|menu\|source\|edit\|viewer\|viewer-zoom\|toolarge` and `?theme=light\|dark`. It draws the touch shell's top bar itself; the drawer and quick search are in `../screens/shared.js`. |
| `attachment-review.html` | The design review: today on a phone, every state as a live frame, scope, what it takes to build, and the decisions. |
| `images/attachment*.png` | `attachment.html` at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-attachment*.png`, `images/today-image-preview.png` | The real attachment page, its Edit view and the Feature's image preview at 390 × 844, before this change (main at 918cf067, the SPA's component harness with the page-shot fixtures). |

### Today

| Rendered | Edit | Image preview |
|---|---|---|
| ![Today: the attachment page on a phone](images/today-attachment.png) | ![Today: Edit on a phone](images/today-attachment-edit.png) | ![Today: the image preview dialog on a phone](images/today-image-preview.png) |

1. The header stacks the back link, a two-line facts line and five buttons, so Replace… wraps onto its own row.
2. The side panel (outline, images, the no-history note, the other Markdown files) falls under the document.
3. A long file has no way to jump between sections.
4. Wide tables and code blocks have no scroll container of their own.
5. Save is off screen below a fixed 560px editor.
6. An image opens in a ~340px dialog with no zoom and no next or previous.
7. The text is 12–13px and the buttons are 28px.

### The phone page

| State | Light | Dark |
|---|---|---|
| Rendered | ![Rendered, light](images/attachment.light.png) | ![Rendered, dark](images/attachment.dark.png) |
| Scrolled: only the top bar stays | ![Scrolled, light](images/attachment-scrolled.light.png) | ![Scrolled, dark](images/attachment-scrolled.dark.png) |
| On this page (Contents pill) | ![Contents sheet, light](images/attachment-contents.light.png) | ![Contents sheet, dark](images/attachment-contents.dark.png) |
| About this file | ![About this file sheet, light](images/attachment-about.light.png) | ![About this file sheet, dark](images/attachment-about.dark.png) |
| Markdown on SHIP-101 | ![File switcher sheet, light](images/attachment-files.light.png) | ![File switcher sheet, dark](images/attachment-files.dark.png) |
| ⋯ More | ![Action sheet, light](images/attachment-menu.light.png) | ![Action sheet, dark](images/attachment-menu.dark.png) |
| Source | ![Source, light](images/attachment-source.light.png) | ![Source, dark](images/attachment-source.dark.png) |
| Edit | ![Edit with the Save bar, light](images/attachment-edit.light.png) | ![Edit with the Save bar, dark](images/attachment-edit.dark.png) |
| Image viewer | ![Image viewer, light](images/attachment-viewer.light.png) | ![Image viewer, dark](images/attachment-viewer.dark.png) |
| Image viewer, zoomed | ![Image viewer zoomed, light](images/attachment-viewer-zoom.light.png) | ![Image viewer zoomed, dark](images/attachment-viewer-zoom.dark.png) |
| Too large to show | ![Too large, light](images/attachment-toolarge.light.png) | ![Too large, dark](images/attachment-toolarge.dark.png) |

The viewer is always dark, so both themes look the same there. The decisions are in [design/README.md › Markdown attachments on a phone](../README.md#markdown-attachments-on-a-phone-ship-715).
