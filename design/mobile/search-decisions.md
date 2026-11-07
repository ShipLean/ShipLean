# SHIP-730 decisions: Retrospectives search on a phone

Decided by Joel on 7 Nov 2026 in the Lavish design review (`design/mobile/search-review.html` on PR #69). Every recommendation was kept, with no notes. Each option not taken can still be viewed in the mock with the `?alt=` shown.

## Review decisions

| | Question | Decided | Why | Not taken (mock) |
|---|---|---|---|---|
| D1 | A result row (six columns don't fit across a phone) | **One row per point, three lines:** category and work item key; the point on two lines with the match marked; theme (↻ when recurring), sprint and author. A fourth line shows "Root cause: …" or "Match in the raw failure log" when that's where it matched | The point reads in full and you can see why it matched; about 7 points on a 390 × 844 screen | One line like desktop: about 17 on screen, but each point cut to ~45 characters and theme, sprint and author gone (`?alt=line`). Grouped under their retro: fewer repeats, but ordered by retro, unlike desktop (`?alt=grouped`) |
| D2 | Where the retro opens (no room for desktop's 380px panel beside the results) | **Full screen:** ✕ at the top left, the retro's type, key and title in the title slot, opened scrolled to the point, which is outlined | The whole height for a long retro; the title says which retro, since the view opens part way down; Back closes it | A tall bottom sheet (88%): less room, and its own sheets stack on top (`?alt=sheet`). Expands under its row: a retro is 1,500px+, so the list jumps far away (`?alt=inline`) |
| D3 | The Filter sheet's controls (six filters, three flags) | **Choice chips** for Category, Level, Written by and Decision; **native selects** for Sprint and Theme; **switches** for No sprint, Unthemed and Recurring themes only. Changes apply at once; the foot has Clear and "Show n points" | One tap per value for the short lists; the long lists use the phone's own picker (SHIP-805 C5's recommendation) | Every filter a native select with checkboxes: least to build, but two taps for each and the choices hidden (`?alt=selects&sheet=filter`) |
| D4 | Showing what's filtered (the filters are in a sheet) | **A chip per active filter** under the bar, ✕ removes it, **Clear all** with two or more | You see what's on and undo one in a tap; a link from Insights shows its theme | The count on Filter only: shorter page, but you open the sheet to see or undo anything (`?alt=nochips`) |
| D5 | Stepping through results | **‹ ›** at the top right of the open retro, with "Result n of m · sprint" under the title | Read results one after another without going back to the list | None: close and tap the next row (`?alt=nostep`) |
| D6 | Tablets, 600–900px | **The phone layout**, rows at full width | Follows SHIP-805 rule 1; one layout to build and test | List and retro side by side from 600px: closer to desktop, but a second layout and a rule 1 change (`?alt=split`) |
| D7 | Measuring it | **Nothing new:** `viewport` on the server's existing `retro.searched` | Compare searches per week on phones and desktop, against the share of Search `screen.viewed` from phones, four weeks before and after | Also a client event when a result is opened (position, whether ‹ › was used): one more event to keep |

## Shared rules the design relies on (SHIP-805)

These come from SHIP-805, not this story. Its open points are drawn as SHIP-805 recommends. If Joel settles them differently, this design follows.

- **C4** (bottom sheet anatomy, open): the Filter, Move to theme and note ⋯ sheets have a grab bar, the title on the left and Close on the right.
- **C5** (native select or sheet, open): Sprint and Theme are native selects.
- **C9** (closing full-screen views, open): the open retro and New action item close with ✕ at the top left.
- Settled rules used: one 900px breakpoint (1), nothing hover-only (5), one toolbar row with Filter (n) (8), 32px targets (13), 16px inputs (14), only the top bar pinned (12), `viewport` on every event (16).

## Smaller choices made in the design (not put to review)

- The placeholder shortens to "Search points, root causes and logs"; the keyboard's key says Search and closes the keyboard. Results still follow typing after desktop's 300ms pause.
- "n points in m retros" sits on the Filter row.
- Opening a retro adds a history entry (`selected`, `point`), so the phone's Back closes it; ‹ › replace the entry instead of adding more.
- No results shows what was searched and a "Clear search and filters" button.
- In the open retro, notes use SHIP-727's touch notes (its D8): the # theme opens Move to theme, ⋯ on your own point opens Move to theme… and Archive, and New action item is the shared full-screen dialog.
- Matching and filtering don't change: the rows, counts and raw-log rule are the server's, as now.

## Mock-only changes

- `retros.html`'s desktop filters were brought in line with the real app: No sprint, Unthemed, Clear, the Undecided / Actioned / Not needed labels, and every filter in the address.
- `?theme=` is both the real app's theme filter and the mocks' light/dark switch, so `shared.js` now takes only `light` or `dark` from it. `screens/retros*.png` were regenerated.
