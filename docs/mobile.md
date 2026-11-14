# Mobile web

What "supports mobile" means for new work in the SPA. The design source is
[SHIP-805](https://prod.shiplean.app/p/SHIP/items/SHIP-805) (the shared rules) and
[`design/README.md` › Touch shell](../design/README.md#touch-shell-ship-713); this page puts them in repo terms. How the
phone tests work is in [`docs/testing.md` › Phone tests](testing.md#phone-tests).

## What "supports mobile" means

- **At 900px wide and below** the screen uses the touch layout: phones and tablets get the same layout. New UI is checked
  at **390×844** (a phone) and at **768** wide (a tablet).
- **Above 900px nothing changes.** Desktop markup, behaviour and screenshots stay as they are. A desktop baseline that
  moves while you build the touch layout means the change leaked upwards.
- Every new screen meets the rules below, and its route is in the route sweep (below) and passes it.

## The rules (SHIP-805, as built, with its rule numbers)

- **Rule 1:** **One breakpoint, 900px inclusive.** Nothing uses 899px or another width. The one exception is
  Retrospectives › Features (SHIP-728 D5): the desktop table from 600px to 900px, a phone list under 600px (`phone:`).
- **Rules 2–3:** **Navigation** is the drawer from the top bar's Menu; every sub-page is in it. The drawer is the only
  side sheet; anything else that slides in is a bottom sheet.
- **Rule 4:** **Menus become bottom sheets; form dialogs go full screen.** The @mention list stays above the comment
  box.
- **Rule 5:** **Nothing is hover-only.** Row actions are always visible; keyboard hints and key caps are hidden at
  `touch:`.
- **Rule 6:** **Safe areas:** the app sets `viewport-fit=cover`; bars, sheets and toasts pad with
  `env(safe-area-inset-*)`.
- **Rule 7:** **No page-level sideways scroll.** Only tabs, Markdown tables and code, the Retrospectives rows, the
  boards' column pager and chips, and the Retrospectives › Features table scroll sideways, each inside its own box.
- **Rule 8:** **⋯ opens an action sheet**, never a popover. Toolbars keep one row: primary action, main control, Filter
  (count), ⋯ More.
- **Rules 11–12:** **Only the top bar is pinned** (48px plus the top safe area). An edit screen's toolbar takes its
  place while editing; nothing else pins at the top.
- **Rule 13:** **Sizes:** buttons, controls, icon buttons and rows are at least `--touch-target` (32px).
- **Rule 14:** **Every text input, textarea and select is 16px** (`--text-input-touch`); smaller makes iOS Safari zoom.
  One base rule in `index.css` gives this at `touch:`; don't override it smaller.
- **Rule 15:** **Save:** the shared `SaveBar` (message · **Discard** · Save), fixed to the bottom while something is
  unsaved.
- Rules 9 (scrim) and 10 (shared pieces) are under [Tokens](#tokens) and [The shared pieces](#the-shared-pieces);
  rule 16 (`viewport` on web events) is under [Measuring](#measuring).

The open points, built to SHIP-805's recommendations:

- **C4** one `Sheet`: grab bar, title on the left, **Close** on the right; scrim tap, swipe down and Esc close it. Picking
  a single value applies and closes. A search field in a sheet is not focused until tapped.
- **C5** a plain list of values stays a native `<select>`; a menu with groups, avatars, search or footnotes is a `Sheet`.
- **C7** Markdown and long text are 15px (`--text-body-touch`) at `touch:`.
- **C9** every full-screen view closes with ✕ at the top left; Cancel only beside a search field.
- **C11** Markdown tables and `pre` scroll in their own box with a right-edge fade; images fit the width and open the
  image viewer.
- **C12** "phones only" means 900px and below.
- **C13** detail pages put ‹ back on the left and Menu on the right; the title slot is the breadcrumb; one optional page
  action before Search.
- **C14** the floating Contents pill on long Markdown files stays, hidden while the Save bar shows.

## Breakpoints in code

- **Tailwind:** `touch:` (900px and below) and `phone:` (under 600px), defined in `src/ShipLean.Client/src/index.css`.
  For tablet-only styling, combine them (`touch:grid-cols-2 phone:grid-cols-1`).
- **Plain CSS** uses only the same two queries: `@media (max-width: 900px)` and `@media (max-width: 599.98px)`.
  `src/lint/cssBreakpoints.test.ts` fails on any other width query in `src/**/*.css`.
- **The lint rule** `shiplean/no-breakpoint-variants` (`src/ShipLean.Client/eslint/breakpoints.js`) bans variants that
  act at or below 900px in string literals and template text: every `max-[…]:`, `max-sm:` … `max-2xl:`, `min-[N]:` with
  N ≤ 900px (rem and em count as 16px), `sm:` and `md:`. Desktop-only steps (`lg:`, `xl:`, `2xl:`, `min-[N]:` above
  900px) are allowed: they never act at touch widths, and converting them would change desktop.
- **The allowlist** (`eslint/breakpointAllowlist.js`) counts the uses that predate the rule, per file, grouped by the
  task or lane that converts them. A listed file must have exactly its count: convert a use and the lint says "lower or
  delete the entry", so you lower the count, and delete the line at zero. Never add a line, and never use
  `eslint-disable` for this rule.
- In code, read the layout with `useTouchShell()` (`src/touch/useTouchShell.ts`): `{ touch, viewport }`, where
  `viewport` is `phone` (under 600px), `tablet` (600–900px) or `desktop`. Prefer the CSS variants; use the hook only
  when the markup differs.

## Tokens

In `src/ShipLean.Client/src/styles/tokens.css` (and `design/tokens.css`, `design/tokens.json`): `--touch-target` 32px,
`--mobile-bar-height` 48px, `--drawer-width` 304px, `--text-input-touch` 16px, `--text-body-touch` 15px and
`--scrim-touch` (`rgb(10 16 28 / .45)`, behind the drawer and every sheet). Light and dark as everywhere else
(`design/README.md`).

## The shared pieces

Built once; reuse them. Never build your own sheet, scrim, Save bar or top bar, and never copy one to tweak it.

- **Top bar and drawer** (`src/touch/TopBar.tsx`, rendered by `Shell.tsx` at `touch:`): a page sets its slots with
  `useTopBar({ title, back, action, pinned })`. Slots merge per key; each caller owns the keys it sets. `pinned` replaces
  the bar while set (an edit toolbar). Menu opens the drawer.
- **Overlays** (`src/touch/`): `Sheet`, `ActionSheet`, `MoreButton` (⋯ → action sheet) and `SaveBar`. The existing
  `components/Dialog.tsx` goes full screen at `touch:` with a touch ✕ and an optional `primaryAction`; every dialog gets
  it for free.
- **Pager and drag** (`src/touch/`): `TouchPager` (sideways pages with a chip row), `useLongPressDrag` and
  `TouchDragGhost`. Layers: top bar 30, SaveBar 40, dialogs 50, drawer 60/61, sheets 65/70, drag ghost 80, image
  viewer 64 (below sheets 65/70 so its ⋯ sheet opens over it).
- **Markdown and images:** `components/Markdown.tsx` (C7, C11) and `src/touch/imageViewer.ts` (`openImageViewer`).

## Test ids

The shared pieces' ids are in `src/ShipLean.Client/src/touch/testIds.ts` (`touchTestIds`, values `touch-…`), pinned to
`src/ShipLean.TestSupport/TestIds/TouchTestIds.cs` by `TestIdPinTests`. A screen that needs a new id adds it to its own
feature's `testIds.ts`, not to `touchTestIds`. The full-screen dialog's touch ✕ is `touchTestIds.dialogClose`; the
desktop × keeps `componentTestIds.dialogClose`.

## Tests

- **Acceptance on a phone:** an acceptance class opts in with `Viewport => AcceptanceViewport.Phone;` (390×844, touch,
  `IsMobile`, scale 1) and keeps the shared host and database. Drive the shell through the `TouchShell` page object
  (`src/ShipLean.AcceptanceTests/Pages/Touch/TouchShell.cs`): `OpenDrawerAsync`, `Sheet(title)`, `CloseSheetAsync`,
  `ActionRow(label)`, `DialogClose(dialog)`, `Chip(label)` and a locator per touch id. It taps, it doesn't click.
- **`TouchRules`:** `ViolationsAsync(page, within)` checks the rules above (sideways scroll inside `within`, 16px
  inputs, 32px hit areas) on the elements inside `within`. Lane tests use this form on their own regions, because
  sibling tasks build the rest of the screen. `ViolationsAsync(page)` checks the whole page; the sweep uses it.
- **The route sweep** (`Lanes/Touch/TouchRouteSweepTests`) opens every route in `ScreenIds.All` at 390×844 on the seed,
  once its data has rendered, and runs the whole-page check.
- **Adding a route:** one line in `Lanes/Touch/TouchRoutes.cs` (its seeded URL and ready ids, which must appear only
  after the data loads). A route that isn't mobile yet goes in `TouchPendingRoutes` with its story key; delete the line
  once the route passes (the sweep fails while a listed route passes).
- **`TouchExemptRoutes`** lists the routes left desktop-only by decision, each with a reason. A route joins it only by
  Joel's decision; it is never the fix for a failing route.
- **Component screenshots:** the CT `phone` project runs `*.phone.spec.tsx` at 390×844 with touch; the `desktop`
  project runs everything else unchanged. Baselines go in `<spec>-snapshots`, light and dark. Whole pages on a phone use
  `phonePageShots` (`src/pages/screenshots/shootPhone.tsx`). Run one spec with `pnpm run test-ct <spec>`; regenerate
  baselines with `scripts/update-page-shots.sh <spec>` from `src/ShipLean.Client`.

## Measuring

Every web request carries `X-ShipLean-Viewport` (`phone`, `tablet`, `desktop`) and the server stamps `viewport` on the
usage events it records (`docs/analytics.md`). Moves record `method` (`drag`, `touch-drag`, `keyboard`, `sheet`,
`pick-up`): a touch drag sends `touch-drag`, a move picked from a sheet sends `sheet`.

## Checklist for new UI

- [ ] Looked at it at 390×844 and at 768 wide, light and dark; above 900px it is unchanged.
- [ ] Only `touch:`/`phone:` (or the two CSS queries); `pnpm run lint` green without touching the allowlist.
- [ ] Controls and rows at least 32px; inputs 16px; nothing hover-only; no sideways page scroll; only the top bar pinned.
- [ ] Menus, ⋯ and pickers use `Sheet`/`ActionSheet`/`MoreButton`; forms use `Dialog` or `SaveBar`; the top bar is set
      with `useTopBar`.
- [ ] Every interactive element has a `data-testid` from its feature's `testIds.ts`.
- [ ] A phone acceptance test checks your region with `TouchRules.ViolationsAsync(page, within)`.
- [ ] The route is in `TouchRoutes` and passes the sweep (not in `TouchPendingRoutes`).
- [ ] Phone screenshots in a `*.phone.spec.tsx`; desktop baselines unmoved.
