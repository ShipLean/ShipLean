# Touch shell (SHIP-713)

The frame every screen sits in, at 900px wide and below: one sticky top bar, a navigation drawer, the project picker as a bottom sheet and quick search taking the whole screen. Above 900px nothing changes. The rules are in [design/README.md › Touch shell](../README.md#touch-shell-ship-713); the page stories under SHIP-707 (SHIP-714 to SHIP-737) build on them.

| File | What it is |
|---|---|
| `index.html` | The design review: today on a phone, every state, the top bar part by part, the rules for every page, drawer against bottom tabs, how to measure it, and the decisions. Open it directly in a browser. |
| `images/<state>.<theme>.png` | The touch shell on the reference screens at 390 × 844, 2× scale, light and dark. |
| `images/today-*.png` | The real app on a phone before this change (main at 918cf067, seeded local instance). |
| `screenshots.sh` | Regenerates `images/<state>.<theme>.png` from `../screens/` with headless Chrome. |
| `work-item.html` | The design review for the **work item page** (SHIP-714): today, every state, the rules, the decisions and the acceptance criteria. Its images are `images/item*.png`, `images/feature*.png` and `images/today-item*.png`, `today-feature.png`. |
| `feature-board.html` | The design review for the **Feature board** (SHIP-722): today, the three layouts considered, every state, the rules, the decisions and the acceptance criteria. Its images are `images/fboard*.png` and `images/today-fboard*.png`. |

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

## Work item page (SHIP-714)

The work item page (stories, bugs, action items, Features and Epics) inside the touch shell, at 900px and below: section chips stick under the top bar, fields become two-column tiles, field menus open as bottom sheets over the shell's scrim, a Save bar shows while something is unsaved, and tables become stacked rows. Above 900px nothing changes. The review page is [`work-item.html`](work-item.html); the decisions (D1–D12) and acceptance criteria are on SHIP-714.

It is built in `../screens/item-touch.css` and `../screens/item-touch.js`, which `work-item.html` and `feature.html` load. These address parameters open its states: `?at=<section>` (with `?shot` it hides what is above the section, because `--screenshot` ignores scrolling; `?at=Conflict` starts at the 409 banner), `?open=state|assignee|parent`, `?dirty`, `?conflict`, `?mention` and `?peek` (the item modal).

### Today

| Story | Story, scrolled | Feature |
|---|---|---|
| ![Today: a story at 390px, the description one letter wide](images/today-item.png) | ![Today: the same story scrolled](images/today-item-scrolled.png) | ![Today: a Feature at 390px, the status band one word a line](images/today-feature.png) |

### Story, bug and action item

| State | Light | Dark |
|---|---|---|
| Top of the page | ![Work item, light](images/item.light.png) | ![Work item, dark](images/item.dark.png) |
| State as a bottom sheet | ![State sheet, light](images/item-state.light.png) | ![State sheet, dark](images/item-state.dark.png) |
| Parent picker | ![Parent sheet, light](images/item-parent.light.png) | ![Parent sheet, dark](images/item-parent.dark.png) |
| Tasks, with unsaved edits | ![Tasks and the Save bar, light](images/item-tasks.light.png) | ![Tasks and the Save bar, dark](images/item-tasks.dark.png) |
| Comment with @mention | ![Discussion with an @mention, light](images/item-discussion.light.png) | ![Discussion with an @mention, dark](images/item-discussion.dark.png) |
| Links, attachments, history | ![The rail sections, light](images/item-rail.light.png) | ![The rail sections, dark](images/item-rail.dark.png) |
| Someone saved first (409) | ![Conflict banner, light](images/item-conflict.light.png) | ![Conflict banner, dark](images/item-conflict.dark.png) |
| Bug: repro and severity | ![Bug, light](images/item-bug.light.png) | ![Bug, dark](images/item-bug.dark.png) |
| Action item | ![Action item, light](images/item-action.light.png) | ![Action item, dark](images/item-action.dark.png) |
| Item modal (`?item=KEY`) | ![Item modal, light](images/item-modal.light.png) | ![Item modal, dark](images/item-modal.dark.png) |

### Feature and Epic

| State | Light | Dark |
|---|---|---|
| Top of the page | ![Feature, light](images/feature.light.png) | ![Feature, dark](images/feature.dark.png) |
| Edit status: Blocked | ![Status editor, light](images/feature-status-edit.light.png) | ![Status editor, dark](images/feature-status-edit.dark.png) |
| Description | ![Feature description, light](images/feature-description.light.png) | ![Feature description, dark](images/feature-description.dark.png) |
| Links | ![Feature links, light](images/feature-links.light.png) | ![Feature links, dark](images/feature-links.dark.png) |
| Stories and bugs | ![Stories and bugs, light](images/feature-children.light.png) | ![Stories and bugs, dark](images/feature-children.dark.png) |
| Log | ![Feature log, light](images/feature-log.light.png) | ![Feature log, dark](images/feature-log.dark.png) |

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

## Backlogs: stories, features and epics (SHIP-716)

The Stories backlog (List, Planning, Tree) and the Features and Epics backlogs at 900px and below, inside the touch shell. Desktop is unchanged.

| File | What it is |
|---|---|
| `backlogs.html` | The phone screen, opened directly. `?level=stories\|features\|epics`, `?mode=list\|planning\|tree`, `?state=more\|search\|add\|filter\|row\|drag\|select\|bulk-sprint\|points\|section` and `?theme=light\|dark`. It draws the touch shell's top bar itself. |
| `backlogs-review.html` | The design review: today on a phone, every state as a live frame, scope, what it takes to build, and the decisions. |
| `images/backlogs*.png` | `backlogs.html` at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-backlogs-*.png` | The real Stories (List, Planning, Tree), Features and Epics pages at 390 × 844 before this change (the SPA's component harness with the page-shot fixtures). |

### Today

| Stories | Planning | Tree | Features | Epics |
|---|---|---|---|---|
| ![Today: Stories on a phone](images/today-backlogs-stories.png) | ![Today: Planning on a phone](images/today-backlogs-planning.png) | ![Today: Tree on a phone](images/today-backlogs-tree.png) | ![Today: Features on a phone](images/today-backlogs-features.png) | ![Today: Epics on a phone](images/today-backlogs-epics.png) |

1. Every page is a desktop table clipped at the right edge, so Points, Sprint, Assignee and Progress are off screen.
2. The toolbar wraps to 2–4 rows, with keyboard hints.
3. Planning's sprint headers squash into cells: dates wrap to five lines and capacity is cut off.
4. Tree indents each level, so titles wrap to 3–4 lines.

### The phone page

| State | Light | Dark |
|---|---|---|
| List | ![List, light](images/backlogs.light.png) | ![List, dark](images/backlogs.dark.png) |
| ⋯ More | ![⋯ More, light](images/backlogs-more.light.png) | ![⋯ More, dark](images/backlogs-more.dark.png) |
| Search this list | ![Search this list, light](images/backlogs-search.light.png) | ![Search this list, dark](images/backlogs-search.dark.png) |
| + New (inline add) | ![+ New (inline add), light](images/backlogs-add.light.png) | ![+ New (inline add), dark](images/backlogs-add.dark.png) |
| Filter | ![Filter, light](images/backlogs-filter.light.png) | ![Filter, dark](images/backlogs-filter.dark.png) |
| A row's ⋯ | ![A row's ⋯, light](images/backlogs-row.light.png) | ![A row's ⋯, dark](images/backlogs-row.dark.png) |
| Drag a row by its handle | ![Drag a row by its handle, light](images/backlogs-drag.light.png) | ![Drag a row by its handle, dark](images/backlogs-drag.dark.png) |
| Select mode | ![Select mode, light](images/backlogs-select.light.png) | ![Select mode, dark](images/backlogs-select.dark.png) |
| Select mode · Sprint | ![Select mode · Sprint, light](images/backlogs-bulk-sprint.light.png) | ![Select mode · Sprint, dark](images/backlogs-bulk-sprint.dark.png) |
| Estimate (? or the banner) | ![Estimate (? or the banner), light](images/backlogs-points.light.png) | ![Estimate (? or the banner), dark](images/backlogs-points.dark.png) |
| Planning | ![Planning, light](images/backlogs-planning.light.png) | ![Planning, dark](images/backlogs-planning.dark.png) |
| Planning · drag into a sprint | ![Planning · drag into a sprint, light](images/backlogs-planning-drag.light.png) | ![Planning · drag into a sprint, dark](images/backlogs-planning-drag.dark.png) |
| Planning · ⋯ More | ![Planning · ⋯ More, light](images/backlogs-planning-more.light.png) | ![Planning · ⋯ More, dark](images/backlogs-planning-more.dark.png) |
| A sprint's ⋯ | ![A sprint's ⋯, light](images/backlogs-planning-section.light.png) | ![A sprint's ⋯, dark](images/backlogs-planning-section.dark.png) |
| Tree | ![Tree, light](images/backlogs-tree.light.png) | ![Tree, dark](images/backlogs-tree.dark.png) |
| Features | ![Features, light](images/backlogs-features.light.png) | ![Features, dark](images/backlogs-features.dark.png) |
| Epics | ![Epics, light](images/backlogs-epics.light.png) | ![Epics, dark](images/backlogs-epics.dark.png) |

The decisions are in [design/README.md › Backlogs on a phone](../README.md#backlogs-on-a-phone-ship-716).

## Boards: stories, features and epics (SHIP-719)

The Stories, Features and Epics boards at 900px and below, inside the touch shell. Desktop is unchanged.

| File | What it is |
|---|---|
| `../screens/board-touch.css`, `board-touch.js` | The touch layer, loaded by `board.html` and `features-board.html`. `?col=<column id>`, `?sheet=filter\|more\|card:KEY\|assign:KEY\|dod:<column id>`, `?drag=KEY&to=<column id>`, `?q=`, `?who=`, `?compact`, `?nolanes`; `?notouch` shows the desktop board at any width. |
| `boards-review.html` | The design review: today, how the board fits a phone, every state as a live frame, the requirements, the decisions and the acceptance criteria. |
| `images/board*.png`, `features-board*.png`, `epics-board*.png` | The boards at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/*-before.*.png` | The same reference boards with the touch layer off: a 1120px board in a 390px window, as the app does today (`BoardGrid.tsx` sets `min-w-[1120px]`). |

### Before

| Stories | Features | Epics |
|---|---|---|
| ![Before: Stories board on a phone](images/board-before.light.png) | ![Before: Features board on a phone](images/features-board-before.light.png) | ![Before: Epics board on a phone](images/epics-board-before.light.png) |

1. The board is 1120px wide, so about one and a half columns show and the rest is a sideways scroll.
2. The toolbar wraps to three rows.
3. Cards move only by HTML drag and drop, which touch screens don't fire, or by Alt+arrow keys.
4. ⓘ and the assignee menu are popovers anchored to small targets.

### The phone boards

| State | Light | Dark |
|---|---|---|
| Stories: New | ![Stories board, light](images/board.light.png) | ![Stories board, dark](images/board.dark.png) |
| Ready: split column, two lanes | ![Ready, light](images/board-ready.light.png) | ![Ready, dark](images/board-ready.dark.png) |
| Dragging a card | ![Dragging a card, light](images/board-drag.light.png) | ![Dragging a card, dark](images/board-drag.dark.png) |
| Card ⋯ | ![Card actions, light](images/board-card.light.png) | ![Card actions, dark](images/board-card.dark.png) |
| Filter | ![Filter, light](images/board-filter.light.png) | ![Filter, dark](images/board-filter.dark.png) |
| ⋯ More | ![⋯ More, light](images/board-more.light.png) | ![⋯ More, dark](images/board-more.dark.png) |
| Assign | ![Assign, light](images/board-assign.light.png) | ![Assign, dark](images/board-assign.dark.png) |
| ⓘ Definition of done | ![Definition of done, light](images/board-dod.light.png) | ![Definition of done, dark](images/board-dod.dark.png) |
| Compact cards | ![Compact cards, light](images/board-compact.light.png) | ![Compact cards, dark](images/board-compact.dark.png) |
| Features: In progress | ![Features board, light](images/features-board.light.png) | ![Features board, dark](images/features-board.dark.png) |
| What's under a Feature | ![Children, light](images/features-board-children.light.png) | ![Children, dark](images/features-board-children.dark.png) |
| Lanes by Epic | ![Lanes by Epic, light](images/features-board-lanes.light.png) | ![Lanes by Epic, dark](images/features-board-lanes.dark.png) |
| Dragging a Feature | ![Dragging a Feature, light](images/features-board-drag.light.png) | ![Dragging a Feature, dark](images/features-board-drag.dark.png) |
| Feature card ⋯ | ![Feature card actions, light](images/features-board-card.light.png) | ![Feature card actions, dark](images/features-board-card.dark.png) |
| Move to Done with open children | ![Move to Done check, light](images/features-board-done-check.light.png) | ![Move to Done check, dark](images/features-board-done-check.dark.png) |
| Epics | ![Epics board, light](images/epics-board.light.png) | ![Epics board, dark](images/epics-board.dark.png) |
| Epic card ⋯ | ![Epic card actions, light](images/epics-board-card.light.png) | ![Epic card actions, dark](images/epics-board-card.dark.png) |

The decisions are in [design/README.md › Boards on a phone](../README.md#boards-on-a-phone-ship-719).

## Sign in on a phone (SHIP-736)

`/login` and the steps around it at 900px and below: sign in (password, with and without Google, and its notes), Choose your own password, No projects yet, and proxy sign-in. There's no touch shell here, since nobody is signed in yet. Desktop is unchanged.

| File | What it is |
|---|---|
| `sign-in.html` | The phone screen, opened directly. `?state=error\|signed-out\|ended\|google-failed\|google-refused\|change\|no-projects\|no-projects-member\|proxy\|proxy-disabled\|proxy-ended`, `?google=0`, `?kb` (a phone keyboard over the bottom 336px), `?typed`, `?inst=<name>`, `?theme=light\|dark`. `?alt=centred\|flat\|side\|h32\|footer\|google-last` show the options the decisions turned down. |
| `sign-in-review.html` | The design review: today on a phone, every state and alternative as a live frame, a tablet, scope, what it takes to build, measuring and the decisions. |
| `images/sign-in*.png` | `sign-in.html` at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-sign-in-*.png` | Before this change: `today-sign-in-prod.png` is prod's `/login` at 390 × 844 on 7 Nov 2026; the others are `../screens/signin.html` in a 390px window, which the SPA matches. |

### Today

| Prod | Failed | Choose your own password | No projects |
|---|---|---|---|
| ![Today: prod sign-in on a phone](images/today-sign-in-prod.png) | ![Today: failed sign-in on a phone](images/today-sign-in-error.png) | ![Today: choose your own password on a phone](images/today-sign-in-change.png) | ![Today: no projects on a phone](images/today-sign-in-no-projects.png) |

1. 13px fields, so iOS Safari zooms the page.
2. The card is centred, so Password and Sign in end up under the keyboard.
3. A 96px label column leaves about 216px for an email address.
4. Sign in is a small button in the footer's corner, after Google.
5. Show/Hide is a 24px target. Email autofocuses and opens the letter keyboard with autocapitalise.
6. The bar is 38px with no safe area; on prod it reads "ShipLean · ShipLean".

### The phone page

| State | Light | Dark |
|---|---|---|
| Sign in, Google on | ![Sign in, Google on, light](images/sign-in.light.png) | ![Sign in, Google on, dark](images/sign-in.dark.png) |
| The keyboard is open | ![The keyboard is open, light](images/sign-in-keyboard.light.png) | ![The keyboard is open, dark](images/sign-in-keyboard.dark.png) |
| Password only (Google off) | ![Password only (Google off), light](images/sign-in-password-only.light.png) | ![Password only (Google off), dark](images/sign-in-password-only.dark.png) |
| Sign-in failed | ![Sign-in failed, light](images/sign-in-error.light.png) | ![Sign-in failed, dark](images/sign-in-error.dark.png) |
| Signed out | ![Signed out, light](images/sign-in-signed-out.light.png) | ![Signed out, dark](images/sign-in-signed-out.dark.png) |
| Session ended | ![Session ended, light](images/sign-in-ended.light.png) | ![Session ended, dark](images/sign-in-ended.dark.png) |
| Google didn't finish | ![Google didn't finish, light](images/sign-in-google-failed.light.png) | ![Google didn't finish, dark](images/sign-in-google-failed.dark.png) |
| Google account refused | ![Google account refused, light](images/sign-in-google-refused.light.png) | ![Google account refused, dark](images/sign-in-google-refused.dark.png) |
| Choose your own password | ![Choose your own password, light](images/sign-in-change.light.png) | ![Choose your own password, dark](images/sign-in-change.dark.png) |
| Choose your own password, part typed | ![Choose your own password, part typed, light](images/sign-in-change-typed.light.png) | ![Choose your own password, part typed, dark](images/sign-in-change-typed.dark.png) |
| No projects yet (can create) | ![No projects yet (can create), light](images/sign-in-no-projects.light.png) | ![No projects yet (can create), dark](images/sign-in-no-projects.dark.png) |
| No projects yet (can't create) | ![No projects yet (can't create), light](images/sign-in-no-projects-member.light.png) | ![No projects yet (can't create), dark](images/sign-in-no-projects-member.dark.png) |
| Proxy: no sign-in reached ShipLean | ![Proxy: no sign-in reached ShipLean, light](images/sign-in-proxy.light.png) | ![Proxy: no sign-in reached ShipLean, dark](images/sign-in-proxy.dark.png) |
| Proxy: account disabled | ![Proxy: account disabled, light](images/sign-in-proxy-disabled.light.png) | ![Proxy: account disabled, dark](images/sign-in-proxy-disabled.dark.png) |
| Proxy: session ended | ![Proxy: session ended, light](images/sign-in-proxy-ended.light.png) | ![Proxy: session ended, dark](images/sign-in-proxy-ended.dark.png) |
| Instance called ShipLean | ![Instance called ShipLean, light](images/sign-in-instance-shiplean.light.png) | ![Instance called ShipLean, dark](images/sign-in-instance-shiplean.dark.png) |

The decisions are in [design/README.md › Sign in on a phone](../README.md#sign-in-on-a-phone-ship-736).

## Feature board (SHIP-722)

The board on a Feature's own page (`/p/SHIP/items/KEY/board`, the Taskboard's grid for one Feature) at 900px and below. It works like the SHIP-719 boards: one task state a page in a sideways pager with state chips above it; every open story or bug is a lane on every page (its compact index card, then its tasks in that state, or None); closed stories wait behind “Show n closed”. A ⋯ on every task opens its action sheet (Move to a state, another story, Assignee, Open), and a long press lifts it for touch drag (to another lane, or the screen's edge for another state). The toolbar is one row: Stories | People, Filter (n), ⋯, + Add. Above 900px nothing changes. The review page is [`feature-board.html`](feature-board.html); the decisions (D1–D12, settled by Joel on 7 Nov 2026) and acceptance criteria are on SHIP-722.

It is built in `../screens/feature-board-touch.css` and `feature-board-touch.js`, which `feature-board.html` loads (it exposes its state as `SL.fb` and fires `fb:render`). These address parameters open its states: `?col=active`, `?sheet=task|story|assign&task=KEY`, `?sheet=filter|more`, `?filter=mei,none`, `?closed`, `?drag=KEY&to=active`, `?add`, `?addtask=KEY`, `?at=KEY` (with `?shot` it hides what is above that lane), plus the board's own `?key=SHIP-103` and `?view=people`.

### Today

| Leaf Feature | Parent Feature | Grouped by People |
|---|---|---|
| ![Today: the Feature board at 390px, New half visible](images/today-fboard.png) | ![Today: a parent Feature at 390px](images/today-fboard-parent.png) | ![Today: grouped by People at 390px](images/today-fboard-people.png) |

### The phone board

| State | Light | Dark |
|---|---|---|
| New, the first page | ![Feature board, light](images/fboard.light.png) | ![Feature board, dark](images/fboard.dark.png) |
| Active | ![Active page, light](images/fboard-active.light.png) | ![Active page, dark](images/fboard-active.dark.png) |
| Long press and drag | ![Touch drag, light](images/fboard-drag.light.png) | ![Touch drag, dark](images/fboard-drag.dark.png) |
| ⋯ on a task | ![Task actions, light](images/fboard-task.light.png) | ![Task actions, dark](images/fboard-task.dark.png) |
| Move to another story | ![Story sheet, light](images/fboard-story.light.png) | ![Story sheet, dark](images/fboard-story.dark.png) |
| Assignee | ![Assignee sheet, light](images/fboard-assign.light.png) | ![Assignee sheet, dark](images/fboard-assign.dark.png) |
| Filter | ![Filter sheet, light](images/fboard-filter.light.png) | ![Filter sheet, dark](images/fboard-filter.dark.png) |
| Filtered | ![Filtered, light](images/fboard-filtered.light.png) | ![Filtered, dark](images/fboard-filtered.dark.png) |
| Show n closed | ![End of the page, light](images/fboard-end.light.png) | ![End of the page, dark](images/fboard-end.dark.png) |
| Closed shown | ![Closed stories shown, light](images/fboard-closed.light.png) | ![Closed stories shown, dark](images/fboard-closed.dark.png) |
| ⋯ More | ![More sheet, light](images/fboard-more.light.png) | ![More sheet, dark](images/fboard-more.dark.png) |
| + Add story or bug | ![Add row, light](images/fboard-add.light.png) | ![Add row, dark](images/fboard-add.dark.png) |
| Parent Feature | ![Parent Feature, light](images/fboard-parent.light.png) | ![Parent Feature, dark](images/fboard-parent.dark.png) |
| A Feature under it | ![Feature band, light](images/fboard-parent-section.light.png) | ![Feature band, dark](images/fboard-parent-section.dark.png) |
| Grouped by People | ![People lanes, light](images/fboard-people.light.png) | ![People lanes, dark](images/fboard-people.dark.png) |

## Sprint backlog and the sprint header (SHIP-724)

The Sprint backlog (`/p/:key/sprints/:id/backlog`) and the sprint header every sprint tab shares (picker, Current, ⋯, goal, tabs) at 900px and below, inside the touch shell. Desktop is unchanged. The decisions (D1–D11, settled by Joel on 7 Nov 2026) are in [design/README.md › Sprint backlog on a phone](../README.md#sprint-backlog-on-a-phone-ship-724) and on SHIP-724.

| File | What it is |
|---|---|
| `sprint-backlog.html` | The phone screen, opened directly. `?sheet=picker\|more\|state:KEY\|ready\|active`, `?edit=goal\|sprint`, `?close`, `?burn=0-4`, `?tasks=off`, `?toast=conflict`, `?at=list` (with `?shot`, hides what is above the list) and `?theme=light\|dark`. It draws the touch shell's top bar itself. The data is the page-shot fixture's Sprint 24. |
| `sprint-backlog-review.html` | The design review: today on a phone, every state as a live frame, what changes in the code, and the decisions. |
| `images/sprint-backlog*.png` | `sprint-backlog.html` at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-sprint-*.png` | The real Sprint backlog at 390 × 844 before this change (the SPA's component harness with the page-shot fixtures, 1×). |

### Today

| The whole page | Sprint picker | State menu | Dark |
|---|---|---|---|
| ![Today: the Sprint backlog at 390px](images/today-sprint-backlog-full.png) | ![Today: the sprint picker popover](images/today-sprint-picker.png) | ![Today: the state menu over the table](images/today-sprint-state-menu.png) | ![Today: dark](images/today-sprint-backlog.dark.png) |

1. The 256px rail (Ready to pick up, In progress now, Points by state) sits on top of the table and hides it.
2. The table keeps six columns, so state, assignee, tasks and points are a sideways scroll away.
3. State menus and the sprint picker are popovers on 24px buttons.
4. In progress now says who is active; what they're on is only a hover title.
5. The header packs the picker, Current, All sprints and Close sprint on one line; Edit and Edit sprint squeeze beside the goal.

### The phone page

| State | Light | Dark |
|---|---|---|
| Top of the page | ![Sprint backlog, light](images/sprint-backlog.light.png) | ![Sprint backlog, dark](images/sprint-backlog.dark.png) |
| The list | ![The list, light](images/sprint-backlog-list.light.png) | ![The list, dark](images/sprint-backlog-list.dark.png) |
| Tasks switched off | ![Tasks off, light](images/sprint-backlog-no-tasks.light.png) | ![Tasks off, dark](images/sprint-backlog-no-tasks.dark.png) |
| Reading the burndown | ![Burndown, light](images/sprint-backlog-burndown.light.png) | ![Burndown, dark](images/sprint-backlog-burndown.dark.png) |
| States sheet | ![States sheet, light](images/sprint-backlog-state.light.png) | ![States sheet, dark](images/sprint-backlog-state.dark.png) |
| Sprint picker | ![Sprint picker, light](images/sprint-backlog-picker.light.png) | ![Sprint picker, dark](images/sprint-backlog-picker.dark.png) |
| Header ⋯ | ![Header actions, light](images/sprint-backlog-more.light.png) | ![Header actions, dark](images/sprint-backlog-more.dark.png) |
| Ready to pick up | ![Ready to pick up, light](images/sprint-backlog-ready.light.png) | ![Ready to pick up, dark](images/sprint-backlog-ready.dark.png) |
| In progress now | ![In progress now, light](images/sprint-backlog-active.light.png) | ![In progress now, dark](images/sprint-backlog-active.dark.png) |
| Edit goal | ![Edit goal, light](images/sprint-backlog-goal.light.png) | ![Edit goal, dark](images/sprint-backlog-goal.dark.png) |
| Edit sprint | ![Edit sprint, light](images/sprint-backlog-edit.light.png) | ![Edit sprint, dark](images/sprint-backlog-edit.dark.png) |
| Close sprint | ![Close sprint, light](images/sprint-backlog-close.light.png) | ![Close sprint, dark](images/sprint-backlog-close.dark.png) |
| Someone else changed it | ![Conflict toast, light](images/sprint-backlog-conflict.light.png) | ![Conflict toast, dark](images/sprint-backlog-conflict.dark.png) |

## Sprint retro on a phone (SHIP-727)

The sprint Retro tab at 900px and below, inside the touch shell. Desktop is unchanged.

| File | What it is |
|---|---|
| `../screens/retro-touch.css`, `retro-touch.js` | The touch layer, loaded by `sprint-retro.html`. `?cat=well\|unplanned\|bad\|improve`, `?sheet=filter\|more\|coverage\|actions\|recurring\|theme:<point>\|note:<point>`, `?open=KEY` (By work item), `?dialog=<point>` (New action item), `?alt=stack\|pager` (the D1 alternatives), plus the page's own `?view=items`, `?decide=open`, `?why=<point>` and `?adding=sprint:<category>`; `?notouch` shows the desktop page at any width. |
| `sprint-retro-review.html` | The design review: today, the three ways to fit four categories, every state as a live frame, what changes, the decisions and the acceptance criteria. |
| `images/sprint-retro*.png` | The Retro tab at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-retro*.png` | The real SPA (main at 918cf067) at 390px and 768px, from the page-shot fixtures in a component harness. |

### Today

| Top of the page | The whole page | By work item | Tablet, 768px |
|---|---|---|---|
| ![Today: the rail drawn over the board](images/today-retro.png) | ![Today: the whole page, 3,300px](images/today-retro-long.png) | ![Today: By work item, 6,900px](images/today-retro-items.png) | ![Today: a tablet](images/today-retro-tablet.png) |

1. The rail is always 18rem wide (`SprintRetroPanel.tsx`: `grid-cols-[minmax(0,1fr)_18rem]`), so at 390px it is drawn over the board.
2. The board gets about 150px; notes wrap every two or three words.
3. The page is 3,300px long, and By work item 6,900px.
4. The toolbar wraps to three rows and is sticky.
5. The numbers and the Recurring chip explain themselves only in hover tooltips; note buttons are 18–24px; text boxes are 11px.

### The phone page

| State | Light | Dark |
|---|---|---|
| Went well (top of the page) | ![Went well (top of the page), light](images/sprint-retro.light.png) | ![Went well (top of the page), dark](images/sprint-retro.dark.png) |
| Not in the plan | ![Not in the plan, light](images/sprint-retro-unplanned.light.png) | ![Not in the plan, dark](images/sprint-retro-unplanned.dark.png) |
| To decide on | ![To decide on, light](images/sprint-retro-decide.light.png) | ![To decide on, dark](images/sprint-retro-decide.dark.png) |
| Not needed: the reason | ![Not needed: the reason, light](images/sprint-retro-why.light.png) | ![Not needed: the reason, dark](images/sprint-retro-why.dark.png) |
| Move to theme | ![Move to theme, light](images/sprint-retro-theme.light.png) | ![Move to theme, dark](images/sprint-retro-theme.dark.png) |
| ⋯ on your own point | ![⋯ on your own point, light](images/sprint-retro-note.light.png) | ![⋯ on your own point, dark](images/sprint-retro-note.dark.png) |
| Coverage | ![Coverage, light](images/sprint-retro-coverage.light.png) | ![Coverage, dark](images/sprint-retro-coverage.dark.png) |
| Action items | ![Action items, light](images/sprint-retro-actions.light.png) | ![Action items, dark](images/sprint-retro-actions.dark.png) |
| Recurring themes | ![Recurring themes, light](images/sprint-retro-recurring.light.png) | ![Recurring themes, dark](images/sprint-retro-recurring.dark.png) |
| Filter | ![Filter, light](images/sprint-retro-filter.light.png) | ![Filter, dark](images/sprint-retro-filter.dark.png) |
| ⋯ More | ![⋯ More, light](images/sprint-retro-more.light.png) | ![⋯ More, dark](images/sprint-retro-more.dark.png) |
| The sprint header's ⋯ | ![Sprint header ⋯, light](images/sprint-retro-sprint.light.png) | ![Sprint header ⋯, dark](images/sprint-retro-sprint.dark.png) |
| By work item | ![By work item, light](images/sprint-retro-items.light.png) | ![By work item, dark](images/sprint-retro-items.dark.png) |
| An item opened | ![An item opened, light](images/sprint-retro-item-open.light.png) | ![An item opened, dark](images/sprint-retro-item-open.dark.png) |
| New action item | ![New action item, light](images/sprint-retro-action.light.png) | ![New action item, dark](images/sprint-retro-action.dark.png) |

The decisions are in [design/README.md › Sprint retro on a phone](../README.md#sprint-retro-on-a-phone-ship-727).

## Retrospective insights on a phone (SHIP-731)

Retrospective insights at 900px and below, inside the touch shell. Desktop is unchanged.

| File | What it is |
|---|---|
| `../screens/insights-touch.css`, `insights-touch.js` | The touch layer, loaded by `retro-insights.html` (which exposes its state and rules as `window.INS`). `?sheet=range\|more\|defs\|defs:<status or verdict>\|theme:<theme>\|rename:<theme>`, `?merge=<theme>,<theme>` (Merge themes), `?dialog=<theme>` (New action item), `?alt=table\|list` (the D1 alternatives), plus the page's own `?view=themes\|actions\|impact` and `?range=6\|3\|all\|w6\|w12`; `?notouch` shows the desktop page at any width. |
| `insights-review.html` | The design review: today, the three ways to fit the themes table, every state as a live frame, what changes, the decisions and the acceptance criteria. |
| `images/insights*.png` | Insights at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-insights*.png` | The real SPA (main at 918cf067) at 390px and 768px, full page, from the page-shot fixtures in a component harness. |

### Today

| Recurring themes | Action items | Changes & impact | Tablet, 768px |
|---|---|---|---|
| ![Today: Status, Action items and Last change off screen](images/today-insights.png) | ![Today: 10 columns](images/today-insights-actions.png) | ![Today: Changes & impact](images/today-insights-impact.png) | ![Today: a tablet](images/today-insights-tablet.png) |

1. The view switch doesn't fit: "Changes & impact" is off screen.
2. The themes table has 13 columns; Status, Action items and Last change are behind a sideways scroll. Theme names wrap every word.
3. The Action items table has 10 columns; titles wrap to five lines.
4. Status, Last change, No sprint and Unthemed explain themselves only in hover tooltips; ✎ and "+ Action item" are about 20px.
5. Merge themes… is a 620px dialog with an 11px select.

### The phone page

| State | Light | Dark |
|---|---|---|
| Recurring themes | ![Recurring themes, light](images/insights.light.png) | ![Recurring themes, dark](images/insights.dark.png) |
| Last 12 weeks | ![Last 12 weeks, light](images/insights-weeks.light.png) | ![Last 12 weeks, dark](images/insights-weeks.dark.png) |
| A theme | ![A theme, light](images/insights-theme.light.png) | ![A theme, dark](images/insights-theme.dark.png) |
| Unthemed | ![Unthemed, light](images/insights-unthemed.light.png) | ![Unthemed, dark](images/insights-unthemed.dark.png) |
| Rename… | ![Rename…, light](images/insights-rename.light.png) | ![Rename…, dark](images/insights-rename.dark.png) |
| What these mean | ![What these mean, light](images/insights-defs.light.png) | ![What these mean, dark](images/insights-defs.dark.png) |
| Range | ![Range, light](images/insights-range.light.png) | ![Range, dark](images/insights-range.dark.png) |
| ⋯ More | ![⋯ More, light](images/insights-more.light.png) | ![⋯ More, dark](images/insights-more.dark.png) |
| Action items | ![Action items, light](images/insights-actions.light.png) | ![Action items, dark](images/insights-actions.dark.png) |
| Changes & impact | ![Changes & impact, light](images/insights-impact.light.png) | ![Changes & impact, dark](images/insights-impact.dark.png) |
| Merge themes | ![Merge themes, light](images/insights-merge.light.png) | ![Merge themes, dark](images/insights-merge.dark.png) |
| New action item | ![New action item, light](images/insights-action.light.png) | ![New action item, dark](images/insights-action.dark.png) |

The decisions are in [design/README.md › Retrospective insights on a phone](../README.md#retrospective-insights-on-a-phone-ship-731).

## Retrospectives search on a phone (SHIP-730)

Retrospectives › Search at 900px and below, inside the touch shell. Desktop is unchanged.

| File | What it is |
|---|---|
| `../screens/search-touch.css`, `search-touch.js` | The touch layer, loaded by `retros.html` (which exposes its state and rules as `window.SRCH`). The page's own filters in the address (`?q`, `?sprint`, `?category`, `?theme`, `?level`, `?role`, `?decision`, `?noSprint`, `?unthemed`, `?recurring`), `?selected=<retro>&point=<point>` (the retro open), `?sheet=filter\|theme:<point>\|note:<point>`, `?dialog=<point>` (New action item), and the alternatives `?alt=line\|grouped` (D1), `sheet\|inline` (D2), `selects` (D3), `nochips` (D4), `nostep` (D5), `split` (D6), several with `+`; `?notouch` shows the desktop page at any width. |
| `search-review.html` | The design review: today, the three result rows, the three places to open the retro, every state as a live frame, what changes, the decisions and the acceptance criteria. |
| `images/retro-search*.png` | Search at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-retro-search*.png` | The real SPA (PR #69 at 3c4b0bc3) at 390px and 768px, full page, from the page-shot fixtures in a component harness. |

### Today

| Search | With a search typed | Tablet, 768px |
|---|---|---|
| ![Today: the results column is 0px wide](images/today-retro-search.png) | ![Today: the filters take 240px and stick](images/today-retro-search-q.png) | ![Today: each point cut to three words](images/today-retro-search-tablet.png) |

1. The results column is 0px wide at 390px (two columns: results and a 380px retro), so no result can be seen or tapped.
2. The filters wrap onto seven rows (about 240px) and stick.
3. The search box and selects are 13px; the checkboxes are 13px targets.
4. The retro panel sticks with its own scroll box.
5. On a 768px tablet each point is cut to three words; the full text is only on hover.

### The phone page

| State | Light | Dark |
|---|---|---|
| All points | ![All points, light](images/retro-search.light.png) | ![All points, dark](images/retro-search.dark.png) |
| Searching “screenshot” | ![Searching, light](images/retro-search-q.light.png) | ![Searching, dark](images/retro-search-q.dark.png) |
| A root-cause match | ![A root-cause match, light](images/retro-search-cause.light.png) | ![A root-cause match, dark](images/retro-search-cause.dark.png) |
| Three filters set | ![Three filters set, light](images/retro-search-chips.light.png) | ![Three filters set, dark](images/retro-search-chips.dark.png) |
| No results | ![No results, light](images/retro-search-empty.light.png) | ![No results, dark](images/retro-search-empty.dark.png) |
| Filter | ![Filter, light](images/retro-search-filter.light.png) | ![Filter, dark](images/retro-search-filter.dark.png) |
| The open retro | ![The open retro, light](images/retro-search-retro.light.png) | ![The open retro, dark](images/retro-search-retro.dark.png) |
| Move to theme | ![Move to theme, light](images/retro-search-theme.light.png) | ![Move to theme, dark](images/retro-search-theme.dark.png) |
| Your own point's ⋯ | ![A point's ⋯, light](images/retro-search-note.light.png) | ![A point's ⋯, dark](images/retro-search-note.dark.png) |
| New action item | ![New action item, light](images/retro-search-action.light.png) | ![New action item, dark](images/retro-search-action.dark.png) |

The decisions are in [design/README.md › Retrospectives search on a phone](../README.md#retrospectives-search-on-a-phone-ship-730).

## Sprint Taskboard (SHIP-725)

The sprint Taskboard (`/p/:key/sprints/:id/taskboard`) at 900px and below, inside the touch shell. Desktop is unchanged. The board works like the Feature board (SHIP-722): one task state a page in a sideways pager with state chips, every story a lane on every page, closed stories one line each, ⋯ and long-press drag. The header is SHIP-724's shared sprint header. What only the Taskboard has: Unparented as the last lane, Pick up always visible, "All tasks done" in the lane, a teammate's move pulsing its state chip, and Taskboard settings full screen. The decisions (D1–D20, settled by Joel on 7 Nov 2026) are in [design/README.md › Sprint Taskboard on a phone](../README.md#sprint-taskboard-on-a-phone-ship-725) and on SHIP-725.

| File | What it is |
|---|---|
| `../screens/taskboard-touch.css`, `taskboard-touch.js` | The touch layer, loaded by `taskboard.html` (which exposes its state as `SL.tb` and fires `tb:render`). `?col=<state>`, `?sheet=task\|story\|assign&task=KEY`, `?sheet=filter\|more\|sprint\|sprintmore`, `?filter=mei`, `?alldone`, `?drag=KEY&to=<state>`, `?bump=<state>`, `?add`, `?addtask=KEY`, `?at=KEY` (with `?shot`), `?view=people`, `?dialog=settings&pane=styles\|tags`. In the mocks it is a copy of `feature-board-touch.*` with the Taskboard's extras. |
| `taskboard-review.html` | The design review: today on a phone, every state as a live frame, the requirements, the decisions and the acceptance criteria. |
| `images/tboard*.png` | `taskboard.html` at 390 × 844, 2× scale, light and dark (made by `screenshots.sh`). |
| `images/today-tboard*.png` | The real Taskboard at 390 × 844 before this change (the SPA's component harness with the page-shot fixtures, 1×). |

### Today

| Stories | People | Taskboard settings |
|---|---|---|
| ![Today: the Taskboard at 390px](images/today-tboard-full.png) | ![Today: grouped by People](images/today-tboard-people-full.png) | ![Today: Taskboard settings](images/today-tboard-settings.png) |

1. Only the Story / bug column and part of New fit; Active, In review and Closed are off to the right.
2. Tasks move only by HTML drag and drop, which touch screens don't fire; Pick up shows only on hover.
3. The toolbar wraps onto three lines and "+ Add story or bug" is cut off.
4. Taskboard settings keeps its two-column dialog, so the rules are cut off at the right.

### The phone page

| State | Light | Dark |
|---|---|---|
| New: the first page | ![Taskboard, light](images/tboard.light.png) | ![Taskboard, dark](images/tboard.dark.png) |
| Active | ![Active, light](images/tboard-active.light.png) | ![Active, dark](images/tboard-active.dark.png) |
| Long press and drag | ![Dragging a task, light](images/tboard-drag.light.png) | ![Dragging a task, dark](images/tboard-drag.dark.png) |
| Task ⋯ (Pick up first) | ![Task actions, light](images/tboard-task.light.png) | ![Task actions, dark](images/tboard-task.dark.png) |
| Move to another story | ![Story sheet, light](images/tboard-story.light.png) | ![Story sheet, dark](images/tboard-story.dark.png) |
| Assignee | ![Assignee sheet, light](images/tboard-assign.light.png) | ![Assignee sheet, dark](images/tboard-assign.dark.png) |
| Filter | ![Filter sheet, light](images/tboard-filter.light.png) | ![Filter sheet, dark](images/tboard-filter.dark.png) |
| Filtered | ![Filtered, light](images/tboard-filtered.light.png) | ![Filtered, dark](images/tboard-filtered.dark.png) |
| End of the page: closed story, Unparented | ![End of the page, light](images/tboard-end.light.png) | ![End of the page, dark](images/tboard-end.dark.png) |
| All tasks done | ![All tasks done, light](images/tboard-alldone.light.png) | ![All tasks done, dark](images/tboard-alldone.dark.png) |
| A teammate's move | ![Chip pulse, light](images/tboard-bump.light.png) | ![Chip pulse, dark](images/tboard-bump.dark.png) |
| Board ⋯ | ![Board actions, light](images/tboard-more.light.png) | ![Board actions, dark](images/tboard-more.dark.png) |
| Sprint picker | ![Sprints sheet, light](images/tboard-sprint.light.png) | ![Sprints sheet, dark](images/tboard-sprint.dark.png) |
| Header ⋯ | ![Header actions, light](images/tboard-sprint-more.light.png) | ![Header actions, dark](images/tboard-sprint-more.dark.png) |
| + Add story or bug | ![Add row, light](images/tboard-add.light.png) | ![Add row, dark](images/tboard-add.dark.png) |
| Grouped by People | ![People lanes, light](images/tboard-people.light.png) | ![People lanes, dark](images/tboard-people.dark.png) |
| Settings: Styles | ![Settings styles, light](images/tboard-settings.light.png) | ![Settings styles, dark](images/tboard-settings.dark.png) |
| Settings: Fields | ![Settings fields, light](images/tboard-settings-fields.light.png) | ![Settings fields, dark](images/tboard-settings-fields.dark.png) |
| Settings: Tag colours | ![Settings tag colours, light](images/tboard-settings-tags.light.png) | ![Settings tag colours, dark](images/tboard-settings-tags.dark.png) |
