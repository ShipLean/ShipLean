# ShipLean design system

ShipLean's house style is the **Nord Frost (deepened)** palette, Inter for all text, **compact** density, and light and dark themes. A planning tool puts a lot of rows on screen at once, so it stays quiet and dense. Colour is used only where it carries meaning: actions, state categories and work item types.

The screens here are reference designs for every page of the v1 domain model: **Backlogs**, **Boards**, **Sprint backlog / planning**, **Sprint Taskboard**, the **work item form**, **Project settings** and **Jira import** ([#1](https://github.com/joeldickson/ShipLean/issues/1)). Each one is a single HTML file that opens directly in a browser, with no build step. Light and dark screenshots sit next to each file.

| File | What it is |
|---|---|
| `tokens.css` | Source of truth: CSS custom properties for colour, type, space and density, plus light/dark theming, work item type colours, state category colours and chart colours. |
| `tokens.json` | The same values in machine-readable form. |
| `screens/shiplean.css` | Shared chrome and controls (side menu, project picker, top bar with presence, toolbar, buttons, list table, rail, dialog, chips, capacity bar). Tokens only. |
| `screens/shared.js` | Shared sample data (project `SHIP`, five people, Sprints 21–26, Epics, Features and the Stories backlog, Sprint 24's tasks, each type's states, the projects list, who's viewing each screen) and helpers. Builds the side menu and top bar. Handles `?theme=` and `?shot`. |
| `screens/backlog.html` | **Stories backlog, List mode**: the ranked backlog with add-at-top, tree mode (show parents), the filter builder and column options. Its Preview panel simulates the real-time states. |
| `screens/features.html` | **Features and Epics backlogs** (`?level=epics`): the same list one and two levels up, with points and progress rolled up from the stories and bugs below. At the Features level, **+ New work item** opens the New Feature dialog (`?dialog=new`). |
| `screens/board.html` | **Stories board**: Kanban columns mapped to states per type, WIP limits, a split Ready column, definition of done, Expedite and Default swimlanes, card style rules and tag colours, and the pull rule on drag. |
| `screens/board-settings.html` | **Board settings** (Stories board): the settings dialog over the board, in the same shell as the Taskboard settings, with Columns (state mapping, WIP, split, definition of done), Swimlanes, Fields, Styles, Tag colours, Annotations and Card reordering. `?pane=` opens a section. |
| `screens/features-board.html` | **Features and Epics boards** (`?level=epics`): the board one and two levels up, with child roll-ups on the cards, Lanes by Epic (`?lanes=epic`), the inline child list (`?open=SHIP-101`) and the confirm before Done with open children (`?confirm=SHIP-102`). |
| `screens/work-item.html` | **Work item form**: one story (or a bug with `?key=SHIP-153`) with its fields, tasks, discussion, links, attachments and revision history, saved with Save. Its Preview panel simulates a 409 when a teammate saves first. |
| `screens/feature.html` | **Feature form** ([#2](https://github.com/joeldickson/ShipLean/issues/2)): Checkout v2 with Goal, the status band (State, Status, Verified at, Blocked by, Parked reason), the description with inline images, Links as cards with live state, grouped attachments, the Log and History. `?band=blocked\|parked\|edit`, `?menu=state`, `?edit`, `?drop` and `?addlink=og\|github\|private` open those states. |
| `screens/attachment.html` | **Markdown attachment**: an attached `.md` rendered on its own page under the work item, with its outline, which images resolved and the replace rule. `?view=source` shows the source. |
| `screens/settings.html` | **Project settings**: General (locked project key, working days), States per type with categories, Members, Sprints & forecast (velocity window and override) and Integrations, with one Save for the whole page. |
| `screens/import.html` | **Jira import**: one Jira project into one ShipLean project in five steps (Connection, Jira project, Project key, Mapping, Run), with progress, an error log and re-runs. `?step=1..5` and `?run=done\|errors` open those states. |
| `screens/planning.html` | **Planning**: the Stories backlog in Planning mode, with sprint sections stacked above the Backlog. |
| `screens/sprint-backlog.html` | **Sprint backlog**: inside one sprint, with tasks, burndown, Ready to pick up and In progress now. |
| `screens/capacity.html` | **Capacity**: the team types a velocity for the sprint and each member's days available; capacity is velocity / people × days / working days, with public holidays lowering the default days. Only the team total is compared with the sprint. |
| `screens/sprints.html` | **All sprints**: the flat sprint list, the velocity chart and the New sprint form. |
| `screens/sprint-end.html` | **Sprint end**: the dialog for unfinished work once a sprint's end date has passed. |
| `screens/taskboard.html` | **Sprint Taskboard**: the sprint's stories and bugs × Task states, Group by Stories / People, and the Taskboard settings dialog. `?view=people` and `?dialog=settings[&pane=styles\|tags]` open those states. |
| `screens/feature-board.html` | **Feature board**: the Taskboard grid for one Feature's stories and bugs in any sprint, opened from **Task board** on the Feature form. A parent Feature gets one board section per Feature in its tree. `?key=SHIP-103` opens a parent Feature, `?view=people` groups by People. |
| `screens/signin.html` | **Sign in**: cookie sign-in with no side menu, plus the states after it. `?state=error\|change\|signed-out\|ended\|no-projects`. |
| `screens/new-project.html` | **New project**: name, the permanent key (suggested, checked live, with free alternatives), start from defaults or another project's settings, members, working days and Sprint 1. |
| `screens/instance.html` | **Instance settings** (instance admins only): General (name, base URL, who can create projects), Users (add, reset password, unlock, admin, disable) and Projects (every project, Join as admin). `?tab=general\|users\|projects`. |
| `screens/profile.html` | **Your profile**: display name, optional photo and avatar colour (`?photo` opens with a photo), theme and density, password, your projects (Leave), sessions and saved views. |
| `screenshots.sh` | Regenerates `screens/<name>.<theme>.png` (1440×900, 2× scale) with headless Chrome. Variants (such as `taskboard-people`, `backlog-tree`, `work-item-bug`, `import-run`) shoot one screen in another state. |
| `logo/` | The logo: the mark, the light and dark lockups, and PNG exports. See [Logo](#logo). |

Open any screen and use the **Preview** panel (bottom left) to switch the theme. The panel is a review aid, not part of the design. `?theme=light|dark` forces a theme and `?shot` hides the panel.

| Screen | Light | Dark |
|---|---|---|
| Stories backlog (List) | ![Stories backlog, light](screens/backlog.light.png) | ![Stories backlog, dark](screens/backlog.dark.png) |
| Stories backlog · tree | ![Stories backlog with parents, light](screens/backlog-tree.light.png) | ![Stories backlog with parents, dark](screens/backlog-tree.dark.png) |
| Features backlog | ![Features backlog, light](screens/features.light.png) | ![Features backlog, dark](screens/features.dark.png) |
| Epics backlog | ![Epics backlog, light](screens/features-epics.light.png) | ![Epics backlog, dark](screens/features-epics.dark.png) |
| Stories board | ![Stories board, light](screens/board.light.png) | ![Stories board, dark](screens/board.dark.png) |
| Board settings · Columns | ![Board settings, Columns, light](screens/board-settings.light.png) | ![Board settings, Columns, dark](screens/board-settings.dark.png) |
| Board settings · Styles | ![Board settings, Styles, light](screens/board-settings-styles.light.png) | ![Board settings, Styles, dark](screens/board-settings-styles.dark.png) |
| Features board | ![Features board, light](screens/features-board.light.png) | ![Features board, dark](screens/features-board.dark.png) |
| Features board · by Epic | ![Features board with lanes by Epic and a child list open, light](screens/features-board-lanes.light.png) | ![Features board with lanes by Epic and a child list open, dark](screens/features-board-lanes.dark.png) |
| Features board · Done with open children | ![Confirm before moving to Done, light](screens/features-board-done-confirm.light.png) | ![Confirm before moving to Done, dark](screens/features-board-done-confirm.dark.png) |
| Epics board | ![Epics board, light](screens/epics-board.light.png) | ![Epics board, dark](screens/epics-board.dark.png) |
| Work item · story | ![Work item form, story, light](screens/work-item.light.png) | ![Work item form, story, dark](screens/work-item.dark.png) |
| Work item · bug | ![Work item form, bug, light](screens/work-item-bug.light.png) | ![Work item form, bug, dark](screens/work-item-bug.dark.png) |
| Feature form | ![Feature form, light](screens/feature.light.png) | ![Feature form, dark](screens/feature.dark.png) |
| Feature · Blocked | ![Feature blocked, light](screens/feature-blocked.light.png) | ![Feature blocked, dark](screens/feature-blocked.dark.png) |
| Feature · Parked, stale | ![Feature parked with a stale status, light](screens/feature-parked.light.png) | ![Feature parked with a stale status, dark](screens/feature-parked.dark.png) |
| Feature · State menu | ![Feature state menu, light](screens/feature-state-menu.light.png) | ![Feature state menu, dark](screens/feature-state-menu.dark.png) |
| Feature · Blocked needs Blocked by | ![Status editor with Blocked by required, light](screens/feature-status-edit.light.png) | ![Status editor with Blocked by required, dark](screens/feature-status-edit.dark.png) |
| Feature · Edit description | ![Description editor with a pasted image and an external image warning, light](screens/feature-edit.light.png) | ![Description editor with a pasted image and an external image warning, dark](screens/feature-edit.dark.png) |
| Feature · Drop files | ![Drop overlay with an outcome per file, light](screens/feature-drop.light.png) | ![Drop overlay with an outcome per file, dark](screens/feature-drop.dark.png) |
| Feature · Add link, public page | ![Add link with an Open Graph preview, light](screens/feature-link-og.light.png) | ![Add link with an Open Graph preview, dark](screens/feature-link-og.dark.png) |
| Feature · Add link, GitHub | ![Add link with a GitHub pull request card, light](screens/feature-link-github.light.png) | ![Add link with a GitHub pull request card, dark](screens/feature-link-github.dark.png) |
| Feature · Add link, private host | ![Add link to a private host, light](screens/feature-link-private.light.png) | ![Add link to a private host, dark](screens/feature-link-private.dark.png) |
| Markdown attachment | ![Markdown attachment rendered, light](screens/attachment.light.png) | ![Markdown attachment rendered, dark](screens/attachment.dark.png) |
| Markdown attachment · source | ![Markdown attachment source, light](screens/attachment-source.light.png) | ![Markdown attachment source, dark](screens/attachment-source.dark.png) |
| New Feature | ![New Feature dialog, light](screens/features-new.light.png) | ![New Feature dialog, dark](screens/features-new.dark.png) |
| Project settings · States | ![Project settings, light](screens/settings.light.png) | ![Project settings, dark](screens/settings.dark.png) |
| Jira import · Mapping | ![Jira import, Mapping, light](screens/import.light.png) | ![Jira import, Mapping, dark](screens/import.dark.png) |
| Jira import · Project key | ![Jira import, Project key, light](screens/import-key.light.png) | ![Jira import, Project key, dark](screens/import-key.dark.png) |
| Jira import · Run | ![Jira import, Run with errors, light](screens/import-run.light.png) | ![Jira import, Run with errors, dark](screens/import-run.dark.png) |
| Planning | ![Planning, light](screens/planning.light.png) | ![Planning, dark](screens/planning.dark.png) |
| Sprint backlog | ![Sprint backlog, light](screens/sprint-backlog.light.png) | ![Sprint backlog, dark](screens/sprint-backlog.dark.png) |
| Capacity | ![Capacity, light](screens/capacity.light.png) | ![Capacity, dark](screens/capacity.dark.png) |
| All sprints | ![All sprints, light](screens/sprints.light.png) | ![All sprints, dark](screens/sprints.dark.png) |
| Sprint end | ![Sprint end, light](screens/sprint-end.light.png) | ![Sprint end, dark](screens/sprint-end.dark.png) |
| Taskboard | ![Taskboard, light](screens/taskboard.light.png) | ![Taskboard, dark](screens/taskboard.dark.png) |
| Taskboard · People | ![Taskboard grouped by People, light](screens/taskboard-people.light.png) | ![Taskboard grouped by People, dark](screens/taskboard-people.dark.png) |
| Taskboard settings · Fields | ![Taskboard settings, Fields, light](screens/taskboard-settings.light.png) | ![Taskboard settings, Fields, dark](screens/taskboard-settings.dark.png) |
| Feature board | ![Feature board, light](screens/feature-board.light.png) | ![Feature board, dark](screens/feature-board.dark.png) |
| Feature board · parent Feature | ![Feature board for a parent Feature, light](screens/feature-board-parent.light.png) | ![Feature board for a parent Feature, dark](screens/feature-board-parent.dark.png) |
| Feature board · People | ![Feature board grouped by People, light](screens/feature-board-people.light.png) | ![Feature board grouped by People, dark](screens/feature-board-people.dark.png) |
| User menu | ![User menu, light](screens/user-menu.light.png) | ![User menu, dark](screens/user-menu.dark.png) |
| Sign in | ![Sign in, light](screens/signin.light.png) | ![Sign in, dark](screens/signin.dark.png) |
| Sign in · failed | ![Sign in failed, light](screens/signin-error.light.png) | ![Sign in failed, dark](screens/signin-error.dark.png) |
| Sign in · one-time password | ![Choose your own password, light](screens/signin-change.light.png) | ![Choose your own password, dark](screens/signin-change.dark.png) |
| Signed in · no projects | ![No projects yet, light](screens/signin-no-projects.light.png) | ![No projects yet, dark](screens/signin-no-projects.dark.png) |
| New project | ![New project, light](screens/new-project.light.png) | ![New project, dark](screens/new-project.dark.png) |
| Instance settings · Users | ![Instance settings, Users, light](screens/instance.light.png) | ![Instance settings, Users, dark](screens/instance.dark.png) |
| Instance settings · General | ![Instance settings, General, light](screens/instance-general.light.png) | ![Instance settings, General, dark](screens/instance-general.dark.png) |
| Instance settings · Projects | ![Instance settings, Projects, light](screens/instance-projects.light.png) | ![Instance settings, Projects, dark](screens/instance-projects.dark.png) |
| Your profile | ![Your profile, light](screens/profile.light.png) | ![Your profile, dark](screens/profile.dark.png) |
| Your profile · photo | ![Your profile with a photo, light](screens/profile-photo.light.png) | ![Your profile with a photo, dark](screens/profile-photo.dark.png) |
| Taskboard settings · Styles | ![Taskboard settings, Styles, light](screens/taskboard-styles.light.png) | ![Taskboard settings, Styles, dark](screens/taskboard-styles.dark.png) |

## Logo

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="logo/shiplean-logo-on-dark.svg">
  <img alt="ShipLean" src="logo/shiplean-logo-on-light.svg" height="48">
</picture>

The brand assets are kept with the website in [ShipLean/shiplean.app](https://github.com/ShipLean/shiplean.app) (`design/brand/`); the files here are copies, so change them there first and copy them over. The mark is a boat: three backlog rows stacked as a sail, widening down to the hull, on the one brand gradient: `#16a089 → #2563c9` (teal to blue) in light and `#3dd6b5 → #5cc8f0` (mint to cyan) in dark. That gradient is for the mark only; the app's UI stays on the tokens above.

| File | Use |
|---|---|
| `logo/shiplean-mark-light.svg` | The mark on light backgrounds: gradient square, white boat. |
| `logo/shiplean-mark-dark.svg` | The mark on dark backgrounds: mint-to-cyan gradient, navy boat. |
| `logo/shiplean-logo-on-light.svg`, `logo/shiplean-logo-on-dark.svg` | The mark with the **ShipLean** wordmark. The wordmark is live SVG text in Inter 700, so it needs Inter installed; outline it before sending it to print. |
| `logo/shiplean-avatar-mark.png` | 512×512 PNG of the light mark, for avatars. |
| `logo/favicon.svg` | One file that switches between the light and dark mark with the visitor's theme. `src/ShipLean.Client/public/favicon.svg` is a copy. |

Leave clear space around the mark of at least half its width. Don't recolour, stretch or rotate it.

## Rules

- **Tokens only.** Every colour, font size, radius, gap and control height comes from a `var(--…)` in `tokens.css`. The one exception is avatar colours: they are identity colours and stay the same in both themes.
- **Both themes, every time.** Build against the semantic tokens and check the screen in light and dark. The theme follows the OS by default and can be forced with `<html data-theme="light|dark">`.
- **Compact by default.** Size things with `--control-height`, `--row-height`, `--bar-height` and `--rail-width`, so `<html data-density="comfortable">` loosens the whole screen in one switch.
- **Accent is for action and state:** primary buttons, focus rings, selection, the current sprint, forecast lines and capacity bars. Use `--color-accent-soft` for selected and hover backgrounds. Surfaces stay neutral.
- **State colour follows the state category, never its name.** States are custom per project, so use `--state-proposed | in-progress | resolved | completed | removed`, shown as a 7px dot plus the state's name. Totals that group by state group by category.
- **Work item types** use `--type-epic | feature | story | bug | task`, only on the 16px type icon (a coloured square with a letter). Never use them for UI state.
- **Charts** use `--chart-committed` and `--chart-completed` (checked for colour-blind separation in both themes). Charts have one y-axis, a legend, direct labels on the latest values, hover tooltips, and a dashed reference line for velocity.
- **Type scale:** `--text-lg` for screen titles, `--text-md` for section headings, `--text-base` for body, `--text-sm` for controls and table cells, `--text-xs`/`--text-2xs` for meta and overlines. Weights 400/500/600 only.

## Paper cards

Story, feature and epic cards are white index cards, bugs are pink, and tasks are yellow post-its. Paper never follows the theme: it is the same in light and dark, and the ink on it is always the light theme's ink. The theme only changes the card's edge, its shadow and the hover/focus ring (which uses the page's accent, `--theme-accent`, not the paper's).

| Token | Value |
|---|---|
| `--paper-index` | `#ffffff` (story, feature, epic) |
| `--paper-bug` | `#ffe0e5` |
| `--paper-postit` | `#fff9d3` (task) |
| `--paper-postit-muted` | `#6e5d22` (meta text on a post-it) |
| `--paper-edge-mix`, `--paper-edge-amount` | edge = paper mixed with `#1c2638` 15% (light) or `#000000` 28% (dark) |
| `--paper-postit-edge-mix`, `--paper-postit-edge-amount` | post-it edge: `#a8860f` 38% (light), `#000000` 28% (dark) |
| `--paper-shadow` | the theme's card shadow |
| `--theme-accent` | the page accent, captured on `:root` before `.paper` overrides `--color-accent` |

Classes (in `src/ShipLean.Client/src/index.css`, unlayered so they beat Tailwind utilities):

- `.paper` is the scope. It sets `--paper` and `--paper-edge`, and re-declares the light theme's colour and type tokens, so `bg-panel`, `text-ink`, `border-edge`, chips, avatars and buttons inside a card resolve to paper and light ink.
- `.paper-bug` and `.paper-postit` go with `.paper` and pick the paper (the post-it also takes its muted text and warm edge).
- `.paper-sheet` draws the sheet on the element: paper background, edge, shadow, and an accent border and 1px ring on hover and accent outline on focus. It wins over `bg-panel` and `border-edge` on the same element; keep `border` for the width.
- `ruleCardStyle(rule, tintPercent = 9)` tints a card by a style rule colour in paper terms; post-its pass 12. It sets `--sheet-tint` (background), `--sheet-edge` (border) and `--sheet-bar` (the 3px left bar, an inset shadow), which `.paper-sheet` reads; without them the sheet falls back to plain paper.

## Decisions these screens encode

These were decided while reviewing the screens on 2026-10-24.

1. **Planning layout: stacked sprint sections** (Jira style), not a side pane or sprint columns.
2. **Capacity is planned for the team as a unit.** The team types one velocity for the sprint and each member's days available (default: the sprint's working days minus the country's public holidays); a member's capacity is `velocity / people in the table × days / working days`, and only `SUM(capacity_points)` for the sprint is compared with committed points and with velocity. Nothing compares a person with the work assigned to them, and there's no per-person load or over-allocation warning. A sprint with no entries plans against velocity.
3. **Work is pulled, not pre-assigned.** Assignment records who is working on something and, through revisions, who did what. **Moving an unassigned item into an In Progress state assigns it to whoever moved it.**
4. **Unfinished work at sprint end: a roll-over prompt** where someone chooses a destination for each item, defaulting to the next sprint.
5. **Sprint totals count stories and bugs only.** Points on tasks are shown for information. Capacity, burndown and velocity use the requirement level.
6. **The forecast fills each future sprint's remaining room first** (team capacity minus committed), then plans later sprints at velocity.
7. All the screens here are in v1.
8. **Navigation is a side menu on the left**, not a top bar: Backlogs, Boards, Sprints (with the current sprint and All sprints under it when you're in Sprints), Jira import and Settings, with the signed-in user at the bottom.
9. **The project picker is the project name at the top of the side menu.** Clicking it, or Ctrl/⌘+P, opens a searchable list of the projects you're a member of (recent first, matching by name or key). Picking one opens the same screen in that project.
10. **Presence is in v1** (it was under Later in #1). The top bar on every live screen shows avatars of the other people who have that screen open, next to the Live dot, up to three and then `+n`. Clicking the avatars lists everyone.
11. **Where the Backlog screen proposal (reviewed 2026-10-24) and these sprint screens disagreed, the sprint screens win.** That means stacked sprint sections rather than a planning side pane, team-level capacity with no per-person load, and Capacity as its own tab.
12. **Tree mode:** dropping a story on another Feature makes that Feature its parent.
13. **Points on Features and Epics:** the item's own estimate, with the sum of everything under it greyed beside it (`20 · Σ26`).
14. **List mode keeps scheduled items** in the list with a sprint chip, so there's one rank order across sprints.
15. **New work item goes in at the top** of the backlog.
16. **An unestimated item counts as 0** in the forecast and is flagged.

**Model follow-ups for #1:** presence needs the hub to track who has which screen open per project (`JoinScreen(projectId, screen)` plus a `PresenceChanged` event, held in memory and never stored). The Backlog screens also need somewhere to keep the velocity override and window (proposed: `project.velocity_window`, `project.velocity_override`) and per-user view preferences (columns, toggles, last filter per level). Tag colours belong to a board, so each backlog borrows the colours of the board at its own level.

**Proposed model addition:** an optional free-text `note` on Sprint Capacity ("Off Thu–Fri"), so a low sprint explains itself when velocity is read later.

**Still open:**
- Which sprint sections in Planning start collapsed. They're all expanded here.
- Whether the sprint goal also shows on the Planning section headers. (The Taskboard has a slim header without it; see below.)
- Sprint length: copy the last sprint's length (as drawn), or add a `sprint_length_days` project setting.

## Shell patterns (every screen)

- **Side menu** (`--sidebar-width`): the ShipLean mark, the **project button** (key badge, name, member count, ▾), then the sections. The active section has a raised surface, and the active sub-item (Sprint 24, All sprints) uses the accent-soft background. The signed-in user sits at the bottom. Under 900px the menu becomes a strip across the top.
- **Project picker:** opens under the project button with the search field focused. With no search it shows **Recent** and then **All your projects** alphabetically. Typing filters by name or key and highlights the match. ↑/↓ and Enter pick, Esc closes. The footer has **+ New project** and **Import from Jira**. Only projects you're a member of are listed, which matches how the hub hides other projects.
- **Top bar:** a breadcrumb (`SHIP › Backlogs › Stories`) on the left, then on the right the **presence stack** and the **Live** dot. The presence stack leaves out yourself, overlaps the avatars and shows `+n` after three. While the socket is reconnecting, the dot turns warn and says Reconnecting.

## Backlog patterns (`screens/backlog.html`, `screens/features.html`)

- **List and Planning are two modes of the Stories backlog.** List is the plain ranked backlog with Sprint and Assignee columns. Scheduled items stay in the list with a sprint chip, and the current sprint's chip is accent. Completed items are hidden unless **Completed** is ticked.
- **+ New work item** opens an inline row at the top with a type menu (only types at this level: User Story or Bug) and a title field. Enter adds it at #1 with the next key and keeps the row open, and Esc closes it. A new item has no estimate, shown as a warn `?`.
- **Show parents** groups rows under Epic › Feature (› nested Feature) header rows. Header rows show their state and the open points under them, collapse with ▼, and aren't ranked here. Stories with no Feature collect under **No parent**. Dropping a row on a Feature header reparents it, and dropping it on a row in another Feature puts it just above that row and takes that row's parent. A toast confirms each move.
- **Filter** opens the Criteria builder: one group of `field · operator · value` clauses joined by **All** or **Any**, with fields from the fixed field list. Active clauses show as chips in the toolbar. While a filter is on, dragging is off (a banner says why) and `#` keeps showing each item's place in the full backlog.
- **Columns** is a per-user, per-level checklist. Key and Title can't be hidden.
- **Real time** (Preview panel actions): a teammate's edit flashes the row and a toast names who did it. When the connection drops, a warn banner offers Retry now, and on reconnect the screen refetches and says how many changes arrived. A **409** on a stale revision shows who changed what, with **Use mine** (resend against the new revision) or **Keep theirs**.
- **Features and Epics levels** have the same toolbar, without Planning mode. **Pts** shows the item's own estimate with the sum of everything under it greyed beside it, in warn when the sum is more than 25% over. **Progress** counts child stories and bugs by state category (done, in progress, not started), including those under nested Features. On the Epics level, a **Features** column counts Features including nested ones.
- **Empty project** (Preview panel): the first-run state offers **+ New user story** and **Import from Jira**, and links to creating Sprint 1.

## Board patterns (`screens/board.html`)

- **Columns are not states.** Each column maps to one state per type (Ready is New for a User Story and Triaged for a Bug). The header's second line shows the mapping, and dragging a card sets the column while the state follows. The toast names both: `SHIP-158 → In progress (state Active)`, or `(state stays New)` when two columns share a state.
- **WIP limits:** the header shows `count/limit` across every swimlane, counting both halves of a split column. Over the limit the count and the header turn danger. Drops are never blocked.
- **Split columns:** Ready is split into **Doing** and **Done** sub-columns that keep the same state. Done (dashed) means ready to be pulled into the next column.
- **Definition of done:** ⓘ on each column opens a popover with the state per type, the WIP note and the column's definition of done rendered from markdown, with a link to edit it in Board settings.
- **Swimlanes:** Expedite (criteria `Priority = 1`) sits above the Default lane. Each lane collapses with ▼ and shows its card count and open points. Dropping a card into the other lane writes the lane's criteria (Expedite sets Priority 1, leaving it sets Priority 2). Swimlanes can be switched off per user.
- **Cards:** type icon, key and points (warn `?` when unestimated), title, the parent Feature with the `☑ 2/4` task roll-up and the assignee, then tags in the board's tag colours. An unassigned card shows a dashed avatar, because work is pulled. **Compact** shows only the key, points, avatar and one line of title.
- **Card style rules:** first match wins. Tag Blocked tints the card pink and tag Spike purple, both mixed from tokens, with a 3px bar on the left.
- **Pull rule:** dropping an unassigned card into a column mapped to an In Progress state assigns it to whoever moved it, and the toast says so.
- **Moving and ordering:** drop on a card to put it just above (this also reorders within a column), or on the empty part of a cell to put it at the end. Alt+←/→ on a focused card changes column and Alt+↑/↓ reorders.
- **Done** shows the 4 most recent cards in each lane, then **Show older**. **+ New item** at the top of New adds a User Story with the next key.
- **Filter:** keyword, people (or Unassigned) and sprint. Hidden cards still count towards WIP, and the filter bar says so.
- **Real time:** a teammate's move slides the card to its new place, flashes it and names who moved it. A board settings change from a teammate flashes the column header. A dropped connection shows a warn banner with Retry now, and on reconnect the board refetches.

## Board settings patterns (`screens/board-settings.html`)

- **Same shell as the Taskboard settings:** a dialog over the dimmed board, with the nav grouped as **Cards** (Fields, Styles, Tag colours, Annotations) and **Board** (Columns, Swimlanes, Card reordering). It's wider (1080px) to fit the column state matrix. `?pane=` opens a section; Columns is the default. The columns, WIP limits, split and definitions of done are the same as `board.html`.
- **Changes are held until Save.** A dot marks each section with unsaved changes, and the footer names them. Save publishes `BoardSettingsChanged` and every open board updates. Cancel drops the changes, and closing with unsaved changes asks you to Save or Cancel first.
- **Columns are not states.** Each column has a name, WIP limit, Split into Doing / Done, a markdown Definition of done (shown behind ⓘ on the column header) and one state per work item type. Two columns can share a state; a state changed elsewhere moves the card to the first column mapped to it. The first and last columns stay in place and have no WIP limit or split.
- **Every state must be on a column.** Each unmapped state (Removed category excepted) gets a warn callout, such as "Bug state Triaged isn't on any column: cards in it would disappear from the board", with a one-click fix (Preview action **Unmap Triaged**). The Columns nav item and the footer show a warning too. It's a warning, not a block.
- **A column with cards can't be deleted;** an inline message says to move the cards first. New columns go in before the last one. Drag ⠿ or Alt+↑/↓ reorders.
- **Swimlanes** are ordered rule boxes: name, colour, and Criteria (field · operator · value, All or Any) or Manual. Criteria lanes are checked from the top and the first match wins; other cards stay in the manual lane they were dragged to, or go to the default lane, which can be renamed and moved but not deleted.
- **Fields** are set per type (User Story / Bug), with up to 10 extra fields from the fixed field list and a live preview card. **Styles** are ordered rules (first match wins) with a colour, bold, italic, underline, Enabled and criteria; preview cards say which rule won. **Tag colours** come from the semantic swatch set (`accent`, `ok`, `warn`, `info`, `danger`, `purple`, `muted`; purple is `--color-purple`, the Spike label colour); the backlog at the same level borrows them. **Annotations** turn on child roll-ups (on the Stories board only Tasks applies). **Card reordering** chooses whether dragging within a column changes backlog order.
- **409 on save** (Preview action): if a teammate saved other sections first, theirs are kept and yours apply on top.

## Features and Epics board decisions

These were decided while reviewing the Features and Epics boards on 2026-10-24.

1. **Roll-ups are counts per child type**, closed over total, with type icons: `S 3/6 · B 1/2 · F 0/1` on a Feature, `F 1/3 · S 7/10 · B 2/3` on an Epic. They count everything below, including under nested Features. Green when all are closed.
2. **A nested Feature is its own card** on the Features board, with its parent Feature on the parent line. The parent's roll-ups include it (`F 0/1`) and the stories and bugs under it.
3. **Clicking a roll-up lists the children inline** on the card (open ones first, then closed, each with its state), with **+ User story** and **+ Bug** (or **+ Feature** on the Epics board) at the bottom.
4. **Lanes on the Features board are a per-user toggle: None or By Epic.** By Epic shows one lane per Epic in backlog order, empty ones included. Dropping a Feature into another Epic's lane makes that Epic its parent. The Epics board has no lanes.
5. **The assignee on a Feature or Epic is its owner, set by hand.** There's no pull rule above the Stories level.
6. **Moving a card into Done with open children asks first.** The dialog lists what's still open, with **Move to Done anyway** or **Cancel**. A teammate's move arrives without asking. In Done, the roll-ups that aren't complete turn warn. (This went against the recommendation, which was to allow it with a toast.)
7. **When the last child closes, the card offers Move to Done.** Nothing moves on its own, the same as the Taskboard's Move to Resolved.
8. **Default columns:** Features board New · Shaping · In progress (WIP 3) · Releasing (WIP 2) · Done; Epics board New · In progress (WIP 3) · Rolling out · Done. Each board has one type, so each column maps to one state. They're editable in Board settings.

**Model changes for #1:** Card Annotation gains a `features` type (the Epics board, and nested Features on the Features board). The Lanes toggle is a per-user view preference, generated from the Epics rather than stored as Swimlane rows.

## Features and Epics board patterns (`screens/features-board.html`)

- **Cards:** type icon, key, the item's own points with the sum of the stories and bugs below greyed beside it (`20 · Σ26`, warn when the sum is more than 25% over), title, parent (Epic, or parent Feature), roll-ups, owner, tags in this board's tag colours. **Compact** drops the parent line and tags but keeps the roll-ups.
- **Feature states drive the Epics board's roll-ups**, so the two boards always agree. Closing a story on the Stories board ticks up its Feature's and Epic's roll-ups in real time (a plain `WorkItemUpdated`).
- **Filter:** keyword, owner (or No owner) and Epic. Features and Epics don't have sprints.
- Everything else works as on the Stories board: WIP counts and danger headers, ⓘ definition of done, drop-above ordering, Alt+arrow keys, Done showing the 4 most recent, + New Feature (or Epic) at the top of New, and the real-time states.

## Work item form patterns (`screens/work-item.html`)

- **A full page under Backlogs**, opened from any backlog row, with `← Back to Stories backlog`. `?key=SHIP-153` shows a Bug.
- **Save model, not real time:** every field edit marks the form dirty, puts an accent dot after that field's label and turns on **Save** and **Discard** in the header (Ctrl+S saves). One save writes one revision. Leaving with unsaved changes is blocked with a toast.
- **Field strip:** State (a menu grouped by state category, including the project's custom states), Assignee (empty means unassigned, because work is pulled), Sprint (Backlog, current and future sprints), Story points (empty shows a warn `?`), Priority 1 to 4, Parent (Features under their Epic, searchable, shown as the Epic › Feature path) and Tags.
- **Picking up:** moving an unassigned item into an In Progress state assigns it to you, with a toast.
- **Main column:** Description in markdown with View and Edit. A User Story has Acceptance criteria as a checklist with an `n/m met` chip; a Bug has Repro steps and Severity (1 Critical to 4 Low, separate from Priority). Then Tasks, where task points show as `—`, and Discussion with @mentions.
- **Saved straight away:** tasks, comments and attachments are their own records, so they don't wait for Save and don't add form revisions. Links are saved with the form.
- **Side column:** Links grouped by type with + Add link; Attachments; History, newest first, each revision listing who changed which field from → to, with Jira changelog revisions marked `from Jira`; and Imported from Jira with the external key when the item came from an import.
- **409 on a stale revision:** if someone saved first, Save is refused and an inline banner lists their changes (`Story points 5 → 8 by Mei Lin, revision 7`), flags fields you both changed, and lists yours. **Reload theirs** drops your changes. **Re-apply mine** puts them on top of the new revision, ready to save again.

## Feature form decisions

These come from [#2](https://github.com/joeldickson/ShipLean/issues/2) and the review of these screens on 2026-10-25.

1. **The Feature gains Goal, Status, Verified at, Blocked by and Parked reason.** Goal sits above the description. Status is one honest line. Verified at is set whenever Status is written or confirmed with **Still true**.
2. **Default Feature workflow: Idea (Proposed), Active, Blocked, Parked (In Progress), Done (Completed).** Blocked needs Blocked by (a person or free text, plus since when). Parked needs a reason and can say what would revive it. Save is refused until the required field is filled.
3. **The status lives in a band under the field strip**, full width, so it's the first thing you read and has room for Blocked by and Parked reason.
4. **Blocked and Parked keep the In Progress dot.** State colour still follows the category; the band is what looks different. Boards that want Blocked red use card style rules (Board settings · Styles), so nothing is hard-coded.
5. **Links move to a section in the main column** on the Feature form, with URL cards two across and bigger previews. The rail holds Attachments and History.
6. **A Markdown attachment opens rendered on its own page** under the work item, with a linkable URL.
7. **Attachments have no version history.** The same name again replaces the file and soft-deletes the old copy; only the description keeps revisions. Limits are **25 MB a file and 250 MB a Feature**. Markdown attachments are **searchable** in the same index as descriptions.
8. **Images resolve against the same work item's attachments.** External image URLs never render (tracking and leak risk); they show a placeholder, and the editor warns before Save. SVG is sanitised.
9. **Link previews:** GitHub pull requests and issues show live state; GitLab does too when an instance GitLab URL is configured. Other public pages get an Open Graph card fetched by the server (public addresses only unless an admin allow-lists the host, 5 s and 1 MB caps, no cookies). A host ShipLean can't reach gets a fallback card, and an agent can report its state with `report_link_state`; the card says who reported it and when.
10. **Versioned drafts collapse to the latest on import** (`design-v0.1…v0.4` → `design-v0.4.md`).

## Feature form patterns (`screens/feature.html`, `screens/attachment.html`)

- **The story form, one level up.** Same header, Save model, field strip and History. The strip swaps Sprint, Story points and Priority for **Epic**, **Slug** (the Feature's unique external key, shown read-only), **Started** and the stories-and-bugs count.
- **Status band:** state pill, the status line, `✓ Verified 2 days ago` (amber `⚠` after 14 days), who set it and whether through an MCP tool. **Still true** confirms it without editing (one revision, Verified at only). **Edit status** opens the band editor; picking Blocked or Parked opens it with the required field focused.
- **Main column:** Goal, Description (View/Edit; pasting an image attaches it and inserts `![](name)`), Links, Stories and bugs (the to-dos, each its own work item), then the **Log**: the Discussion, newest first, with agent entries marked `Agent · via MCP`. **⋯ → Mark superseded** strikes an entry through and keeps it.
- **Task board** (right of the Stories and bugs header) opens the Feature board for this Feature and the Features under it.
- **Links section:** work item links as rows, then URL cards grouped Code and Pages. A card shows the number, title, live state chip (Open, Draft, Merged, Closed), where it lives and where the state came from (`live · 4 min ago` or `reported by <agent> · 3 h ago`), plus the optional one-line note. **+ Add link** takes a pasted URL and shows the preview it will get before adding.
- **Attachments rail:** grouped by what opening them does: Markdown (opens rendered), Images (preview), Files (download). Dropping files anywhere on the form shows each file's outcome first; a name that already exists is flagged **Replace**.
- **New Feature** (`features.html?dialog=new`): Title, Epic, Slug (suggested from the title until edited, checked live), Goal, Start as Idea or Active, and a first Status line. `create_feature` takes the same fields.

## Settings patterns (`screens/settings.html`)

- **One draft, one Save.** A tab list on the left (General, States, Members, Sprints & forecast, Integrations) edits one draft of the project's settings. The header shows `N unsaved changes`, which lists every change and jumps to it. Tabs with changes get a dot, changed rows get a warn edge, and Discard puts everything back. Ctrl/⌘+S saves, and Save with errors jumps to the first one.
- **General:** name and description are editable. The project key `SHIP` is shown locked, with the reason: it is set at creation and never changes, so `SHIP-123` links never break. The next item number (`SHIP-184`) is information only. Working days are Mon to Sun toggles, with at least one on.
- **States** (the only process customisation in v1): pick a type, then rename a state in place, change its category, drag to reorder (or Alt+↑/↓) and add or delete states. The dot's colour follows the category, never the name. The order is the order in state menus, and for Task the order of the Taskboard columns. SHIP's custom states are Bug `Triaged` (Proposed) and Task `In review` (Resolved).
- **Rules for states:** names are unique per type. Every type keeps a Proposed state (new items start in the first one) and a Completed state, so the last of either can't be deleted. Deleting a state that has items asks where to move them first, and the move happens on Save.
- **Board mapping on States:** each row shows the board column mapped to that state. A new state shows Not mapped with a link to Board settings.
- **Members:** role (Admin or Member), joined date and open items. Removing someone waits for Save and can be undone. The last admin can't be removed or made a Member.
- **Sprints & forecast:** links to All sprints. The velocity window (last N sprints, default 3) and the velocity override (blank uses the calculated velocity) show the resulting forecast velocity and which sprints are in the window.
- **Integrations:** the Jira connection summary with Test connection, managed in Jira import.
- **Read-only for members** (Preview panel): someone who isn't an admin sees every tab with the controls disabled, and a banner naming who to ask.

## Jira import patterns (`screens/import.html`)

- **One Jira project into one ShipLean project**, never a whole Jira site. A stepper runs across the top: **Connection · Jira project · Project key · Mapping · Run**. Each step shows a one-line summary and can be clicked. Next is disabled until the step is valid.
- **Connection:** Jira Cloud (email and API token) or Server / Data Center (personal access token). The token is encrypted at rest and never shown again, so a saved one shows its last four characters and **Replace**. **Test connection** shows who you're connected as, or the error with what to check.
- **Jira project:** a searchable table with key, name, type, lead, issue count and last updated. A project imported before shows `Imported → MOB`, and picking it re-runs that job with its key fixed.
- **Project key:** suggested from the Jira key with digits removed and trimmed to 4 letters (`PLATFORM` → `PLAT`), checked live (2 to 4 letters A to Z, unique). The example shows `PLATFORM-123 → PLAT-123` and the next number after the highest imported one. The target is a new project or an existing empty one.
- **Mapping**, reviewed before the run, with a warnings strip first (warnings never block the run): **Issue types** (Epic → Feature, Story → User Story, Bug → Bug, Task → User Story, Sub-task → Task, or Don't import); **Statuses → States** grouped by ShipLean type, mapping to an existing state or **Create new state** with the category from Jira's status category; **Users** matched by email, with Create user, Same person as, or Attribute to "Jira import"; and **Fields** (story points field, tags, sprints, parent, links, comments, attachments, changelog).
- **Run:** progress overall and per entity, a live log, and an error log with **Retry** per row or **Retry all**. An item that keeps failing offers **Skip**.
- **Re-runnable:** a second run matches on External Ref and updates items instead of duplicating them, adds issues that are new in Jira, and never deletes anything. **Import jobs** lists each job with **Re-run**.

## Open questions from these pages

- Board: should dropping into a criteria swimlane write the field (as drawn), or should criteria lanes refuse manual drops?
- Board: should Done hide items closed in past sprints by count (as drawn) or by age?
- Settings: may members change board settings, or only admins (project settings are admin only as drawn)?
- Work item form: should comments, attachments and task changes appear in History, or only field changes (as drawn)?
- Jira import: should a re-run reuse the saved mapping as it is, or show Mapping again for new statuses and users?
- Board settings: changes wait for Save, while the Taskboard settings apply straight away. Which should both do?
- Work item form: Links moved to the main column on the Feature form. Should the story and bug form follow, so the two stay alike?
- Sample data: `shared.js` still gives Features the story states (New, Active). Moving them to the default Feature workflow changes the backlog and board screenshots, so it's left for the slice that builds it.

## Planning screen patterns (`screens/planning.html`)

- **A mode of the Stories backlog, not a separate screen:** a **List / Planning** switch next to the Epics · Features · Stories level picker. List (`backlog.html`) is the plain ranked backlog. Planning adds the sprint sections above it.
- **Sections:** the current sprint, then each future sprint, then **+ Create sprint**, then **Backlog**. Past sprints don't appear here; they live under All sprints. Every section is the same `backlog_order` filtered by `sprint_id`, so rank is the same everywhere. Numbers restart in each section.
- **Section header:** ▼ collapse, the name, a `Current` chip, dates and item count, then a capacity bar with `committed / team capacity pts` and either `n free` or `+n over` in danger. A sprint with no capacity entries shows a muted bar and a `velocity` chip. Over capacity is a warning only; drops are never blocked.
- **Moving work:** drag a row into another section to plan or unplan it (writes `sprint_id`). Drop on a row to put it just above that row (also writes `backlog_order`), or drop on a section header to change only the sprint. An empty sprint shows a dashed drop zone. With the keyboard, **Alt+↑/↓** on a row's ⠿ grip moves it past its neighbour, crossing into the next section at either end. A toast confirms every move.
- **Forecast** (toggle, on by default): shown in the Backlog section only. The header says `Forecast at velocity 21 (average of last 3 sprints)` and has **Override**. A labelled accent line (`→ Sprint 26`) marks the last item that fits each sprint.
- **Real time:** a teammate's move animates in place with a brief highlight and a toast naming who moved what. A stale write (409 on `revision`) refetches the row.

## Sprint screens patterns

- **Sprint header** (Sprint backlog, Capacity; the Taskboard uses a slim version): a sprint picker (`Sprint 24 ▾`), `Current` or `Ended` chip, dates, working days left, the goal with Edit, and a link to All sprints. Tabs underneath: **Backlog · Taskboard · Capacity**.
- **Sprint backlog** (`screens/sprint-backlog.html`):
  - KPIs: `committed / team capacity pts` (with an edit link to Capacity), completed points, and a **burndown** (points remaining against a dashed ideal line, read from revisions).
  - The table lists stories and bugs in backlog order, with tasks expandable under each. State is changed in place from a compact menu. Task rows show `—` for points.
  - The rail shows **Ready to pick up** (unassigned and not started, with the parent), **In progress now** (each member and how many items they have active, with no comparison to capacity) and **Points by state** (grouped by category).
- **Capacity** (`screens/capacity.html`):
  - One row per member with a points field and an optional note. The footer total is the **team capacity**.
  - The summary shows team capacity, committed and velocity, a bar, the room left (or how far over), and a sense check against velocity: in line within 10%, otherwise a warn callout.
  - **Copy from Sprint 23** fills the numbers from the previous sprint; **Clear** goes back to velocity.
- **All sprints** (`screens/sprints.html`):
  - The velocity chart shows committed vs completed for the last five sprints, with the velocity line.
  - The table runs newest first, with Status computed from the dates. Team capacity shows `21 · velocity` when nobody has entered any, and a dotted underline when a member left a note.
  - The **New sprint** rail form defaults to the next number, starts the next working day after the last sprint, and has the same length as it. The checkbox copies each member's capacity. Overlapping dates are rejected inline.
- **Sprint end** (`screens/sprint-end.html`):
  - It opens over the ended sprint the first time anyone opens a Sprints or Backlog screen after the end date, if stories or bugs whose state category isn't Completed are still in it.
  - It lists each unfinished item with its state, points, open tasks and assignee, and a destination menu: **Move to Sprint 25** (default), **Back to top of backlog** or **Leave in Sprint 24**. **Set all to** buttons change every row at once.
  - A live callout shows the next sprint's total against its team capacity, as a warning when it goes over. A second callout explains that the ended sprint's history doesn't change.
  - **Apply** is done once for everyone, in real time. **Decide later** leaves a warn banner on the sprint with **Review**.

## Taskboard decisions

These were decided while reviewing the Taskboard on 2026-10-24.

1. **Columns are the Task states, one to one,** in state order with Removed left out. There's no separate column mapping on the Taskboard; to change the columns, edit the project's Task states. (SHIP's custom `In review` state in the Resolved category shows a fourth column.)
2. **Group by Stories or People,** plus a face filter. People rows show only the name and a task count, never points or anything compared with capacity.
3. **When a row's last task closes, the row offers Move to Resolved.** The story doesn't change on its own; the prompt can be dismissed.
4. **Rows for completed stories start collapsed** to one line, with per-column counts.
5. **Pick up** on an unassigned card assigns you and moves it to the first In Progress state.
6. **Dragging a task to another row changes its parent.**
7. **Slim sprint header:** name, Current chip, dates and days left on one line. No KPIs, burndown or goal; those stay on the Backlog tab, so the board gets the height.
8. **A Task's parent is optional.** Unparented tasks sit in their own row at the bottom of the board and carry no sprint points.

**Model change from decision 8:** issue #1's hierarchy table should read Task → parent User Story, Bug, or none.

**Still open:**
- The Sprint backlog screen doesn't list unparented tasks yet. They could go in a last "No parent" group there too.

## Paper cards decision

Decided in [issue #3](https://github.com/ShipLean/ShipLean/issues/3), and drawn on the Board, Features/Epics board and Taskboard screens (tokens above). Cards are paper in both themes, not themed surfaces: white index cards for stories, features and epics, pink for bugs, yellow post-its for tasks (paler than a stock canary so text stays easy to read), with the same light-theme ink in dark mode. A style rule tints the paper and keeps the 3px left bar. The task's type icon stays on its post-it. On the Taskboard the story or bug row header is drawn as an inset index card; the Unparented row and the People rows stay plain.

## Taskboard patterns (`screens/taskboard.html`)

- **Rows:** the sprint's stories and bugs in `backlog_order`, then **Unparented** (always last, even when empty, so there's somewhere to drop or add a task). **Columns:** the Task states. Header cells show the state dot and the count of visible cards.
- **Row header** is the story or bug card: ▼ collapse, type icon, key, points, title, state (changed in place from a menu), assignee and a `☑ done/total` bar. A collapsed row is one line, with counts in each column.
- **Task card:** type icon and title, then the fields turned on in settings (key, tags, points, assignee). Grouped by People, the card also shows its parent (or "No parent"). Style rules tint the card and can bold the title; tags use the Taskboard's tag colours. Unassigned cards show a dashed avatar, and **Pick up** on hover or focus.
- **Moving work:** drag a card to another cell. The column writes `state_id`; the row writes `parent_id` (Stories) or `assigned_to` (People; dropping on Unassigned clears it). Dropping on a card puts it just above that card. With the keyboard, **Alt+←/→** changes column and **Alt+↑/↓** changes row. Moving an unassigned task into an In Progress state assigns whoever moved it. A toast confirms every move.
- **+ Task** sits in the first column of each row (including Unparented). Enter adds the task and keeps the box open for the next; Esc closes it.
- **Toolbar:** sprint tabs, **Group by** Stories / People, the face filter (people plus Unassigned; hides other cards), Filter, **⚙ Settings** and **+ Add story or bug**.
- **Settings dialog** (same shell as Board settings; Card Field Setting, Card Style Rule and Tag Colour rows stored against the Taskboard): **Fields** for task cards and for the story/bug row headers; **Styles** as ordered rules (first match wins) with a colour, bold and Criteria clauses; **Tag colours** from the semantic token set. **Columns** is shown disabled, pointing at Task states. Changes apply to the board straight away.
- **Real time:** a teammate's move animates into place with a highlight and a toast naming who did what. Remote events carry state and parent, never a row. A stale write (409 on `revision`) snaps the card back and refetches it.

## Feature board decisions

These were decided while reviewing the Feature board on 2026-10-28.

1. **A Feature board is the Taskboard scoped to a Feature, not a sprint.** Rows are the Feature's stories and bugs in any sprint or the Backlog. Columns are the Task states, as on the Taskboard. It reuses the Taskboard's grid, row headers, cards and card settings (Fields, Styles, Tag colours); there is no second settings document.
2. **The entry point is Task board, right-aligned in the Stories and bugs header** on the Feature form. The page is called **Feature board**.
3. **A parent Feature gets one board section per Feature in its tree**, depth first and indented by depth, with its own stories first ("Directly under …"). The page header rolls up the whole tree, and jump chips go to each section.
4. **Grouped by People, it is one grid for the whole tree,** not one per section, so it answers "who's on what" in one place. On a tree with more than one Feature, each card shows Feature › story above its title.
5. **Same editing as the Taskboard:** drag between cells, + Task and the state select. **A move never changes a sprint**: the column writes the state, the row writes the parent (any story or bug in the tree) or the assignee.
6. **Closed stories and bugs start collapsed,** as on the Taskboard.
7. **No sprint filter.** Each row header shows a sprint pill (the current sprint tinted, `Backlog` dashed), and the header lists the sprints the tree spans.
8. **No Unparented row.** A Feature board holds only tasks that have a story or bug in the tree.

## Feature board patterns (`screens/feature-board.html`)

- **Header:** ← back to the Feature, type icon, key, title and state, `N Features under it` on a parent, then the roll-up for the tree: stories and bugs closed, points closed (bar), tasks closed and the sprints it spans.
- **Toolbar:** Group by Stories / People, the face filter, ⚙ Settings (the Taskboard's dialog) and + Add story or bug (added under this Feature). No sprint tabs.
- **Sections** (parent Feature only): a header with ▼ collapse, type icon, key, title, `in <parent>` when nested two deep, state, `closed/total stories`, `closed/total pts` and Open Feature ↗. Each section has its own column headers, sticky while it scrolls. A Feature with no stories or bugs yet shows one line.
- **Row header:** the Taskboard's story card plus the sprint pill after the key. A story with no tasks still gets a row, showing `No tasks yet` and + Task.

## Account and admin decisions

These were decided while reviewing the account and admin screens on 2026-10-24.

1. **The first admin comes from environment variables on first boot**: `SHIPLEAN_ADMIN_EMAIL` and `SHIPLEAN_ADMIN_PASSWORD`. There's no first-run setup page and no sign-up.
2. **Everyone can create projects**, and becomes the new project's Admin. An instance setting can limit it to instance admins.
3. **A new project starts from ShipLean's defaults or copies settings from a project you're in**: states, board and Taskboard settings, working days and the velocity window. Never work items, sprints, members or the Jira connection.
4. **Sprint 1 is optional on the New project page, and on by default**, with a start date and length. New sprints copy the length of the one before (which settles the open sprint-length question).
5. **Instance settings live in the user menu**, for instance admins only.
6. **Instance admins see every project in Instance settings** but open one only by joining it. **Join as admin** adds them to its Members, where everyone can see it.
7. **People created by Jira import are real users with no password** until an admin uses **Set up sign-in**.
8. **Forgotten passwords: an admin resets them and hands over a one-time password.** ShipLean sends no email in v1.
9. **Avatar: a colour, plus an optional uploaded photo.** The colour sits behind the initials, or behind the photo while it loads and wherever it can't be shown. (Went against the recommendation of colour only.)
10. **Only instance admins can change someone's email.** It's read-only on Your profile.

**Model changes for #1:** Instance settings gains `project_creation` (everyone | admins). User gains sign-in fields (password hash, `must_change_password`, `security_stamp`, failed attempts, `locked_until`, `disabled_at`, `last_sign_in_at`), `avatar_colour`, an optional avatar photo (stored like an attachment), and preferences (theme, density). A user created by Jira import has no password. Users are never hard-deleted; the last active instance admin can't be disabled or demoted, and nobody can do either to themselves. The screen table gains Sign in, New project, Instance settings and Your profile.

## Account and admin patterns

- **User menu:** the signed-in user at the bottom of the side menu is a button. It opens upwards with your name and email, **Your profile**, **Instance settings** (instance admins only, with an Admin chip) and **Sign out**. Pages that belong to the install or to you (New project, Instance settings, Your profile) start the breadcrumb at `ShipLean` instead of the project key and have no Live dot; the side menu keeps the project you came from.
- **Sign in** (`screens/signin.html`): email and password. No side menu; the top bar shows the instance name. No sign-up and no reset email: the footer says to ask your ShipLean admin. A failed sign-in always shows the same sentence (unknown email, wrong password, locked, disabled, rate limited). Signing in with an admin's one-time password goes to **Choose your own password** before anything else. Signed out and session ended (password changed or reset) show a note above the form. Someone in no projects gets **You're not in any projects yet**, with + New project and Import from Jira.
- **New project** (`screens/new-project.html`): opened from + New project in the project picker. The key is suggested from the name until you type your own (one word: its first 4 letters; several: initials), is forced to uppercase, and is checked live (2 to 4 letters, A to Z, unique). A key used by a project you're not in says only "another project". Up to three **other free keys** are offered. The rail shows `CSH-1` and says the key is permanent; the Create button names it (`Create CSH`). **Start from** ShipLean defaults or another project's settings (states, board and Taskboard settings, working days, velocity window; never work items, sprints, members or the Jira connection). You're the Admin. **Create Sprint 1** is optional and on, with start date and length. Create opens the empty backlog.
- **Instance settings** (`screens/instance.html`): General is one draft with Save (name, base URL checked against the address in use, who can create projects, and the fixed sign-in rules). On **Users** each action applies at once: + Add user and Reset password show a **one-time password once**, to hand over yourself, since ShipLean sends no email. Statuses: Active, One-time password not used yet, Locked (Unlock), Can't sign in yet (created by Jira import; **Set up sign-in**), Disabled. You can't disable yourself or remove your own admin rights, one active admin always remains, and there is no delete. **Projects** lists every project on the install, flags one with no active admin, and offers **Join as admin**.
- **Your profile** (`screens/profile.html`): display name, an optional photo (Upload, Replace, Remove; PNG, JPEG or WebP up to 2 MB, cropped to a circle) and a colour, with a preview on a card and in the presence stack, read-only email, theme and density (applied at once and stored on the account), password change (signs out other sessions), your projects with Leave (the only admin can't), Sign out everywhere else and Reset saved views.

## Screenshots

```sh
design/screenshots.sh            # every screen, light and dark
design/screenshots.sh planning   # just one (variants such as taskboard-people work too)
```

This needs headless Chrome or Chromium. Set `CHROME=/path/to/chrome` if it isn't on `PATH`. If symbols such as ⓘ come out as boxes, the host has no symbol font: point `FONTCONFIG_FILE` at a config that adds one (Segoe UI Symbol or Noto Sans Symbols 2). The script loads each screen with `?shot&theme=<light|dark>` at 1440×900, 2× scale.
