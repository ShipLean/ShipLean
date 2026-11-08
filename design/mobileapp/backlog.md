# Backlog (phone app)

Mock-ups: `backlog.html` (`?state=<id>`). Example: the SHIP backlog at Feature and Story level.

## Purpose

See the order of work and change it. Priority is backlog rank, so reordering is the main job here, and voice is the fastest way to do it: "move SHIP-906 above SHIP-907". A Move sheet is the touch fallback. The screen also finds what waits on you and captures new items quickly.

## What's on screen

- **Top bar**: project, Backlog, search.
- **Level**: Epics / Features / Stories segmented control. Filter chips under it: **Needs me** (with a count), Open, and an Epic picker.
- **Rows in rank order**: rank number, title, type, key, state, health when not on track, and an orange **needs** marker (a check for decisions or a speech bubble for questions, with a count). An orange bar on the left edge marks rows that wait on a human.
  - Features show a progress bar and "stories done of total".
  - Stories show the owner, the task counts with blocked ones in red, and their Feature.
- **Move button** (↑↓) on every row opens the Move sheet.
- The tab bar's hold-to-talk button works on the whole list.

## States

| id | What it shows |
|---|---|
| `features` | Seven open Features in rank order, three of them waiting on you. |
| `stories` | Stories with owner, task counts (1 blocked) and their Feature; one waits on you. |
| `listening` | "Move SHIP-906 above SHIP-907", keys as hotwords, three example phrases. |
| `preview` | The move as before → after for each row that shifts (4 → 2, 2 → 3, 3 → 4), with a warning that SHIP-906 is blocked by SHIP-907. |
| `applied` | SHIP-906 at rank 2, highlighted and marked "↑ from 4"; Undo toast. |
| `move` | Move sheet from a row's button: Top, Bottom, Up one, Down one (each says the rank it lands on), Above or below another item. |
| `needs-me` | Needs me filter: only Features that wait on you, each with the question or decisions inline. |
| `capture` | "Add a story to SHIP-906: show a banner while the models download, and put it at the top" previewed as a new Story plus a rank change. |
| `which` | "Put the sign-in story at the top" matches two Stories; numbered choices, tap or say "the first one". |

## Voice actions on this screen

The model gets the level, the visible items (key, title, rank, state, parent), the filters and the project's people.

| Action | What it does | Example |
|---|---|---|
| `move_relative` | Moves an item directly above or below another. | "Move SHIP-906 above SHIP-907" |
| `move_to` | Moves an item to the top, the bottom or a given rank. | "Put the sign-in story at the top" |
| `move_by` | Moves an item up or down a number of places. | "Move SHIP-892 down two" |
| `add_item` | Creates a Story, Bug or Feature under a parent; the title is copied word for word. | "Add a story to SHIP-906: show a banner while the models download" |
| `filter` | Turns on Needs me, a state or an Epic filter. | "Show what needs me" |
| `switch_level` | Switches between Epics, Features and Stories. | "Show stories" |
| `approve_decision` | Confirms a decision on a named item. | "Approve D1 on SHIP-907" |
| `add_note` | Leaves a word-for-word note on a named item. | "Note on SHIP-892: skip the mobile screens for now" |
| `open` | Opens an item. | "Open SHIP-906" |
| `ask` | Answers a question about the visible list, read-only. | "What's blocked?" |

## Left out compared with the web

- Drag and drop. Reordering is by voice or the Move sheet.
- Sprint planning (moving items into sprints), points, bulk select, bulk edit.
- Planning and Tree views; column sort and column choice.
- Tasks at any level of the list.
- Editing any field from a row other than rank.

## Decisions for Joel

- **D1. Should the level control offer Epics, Features and Stories?** Recommended: all three, since the app is about those three levels. Alternative: Features and Stories only, with Epics reached from a Feature's parent.
- **D2. Should the rank preview list every row whose position changes?** Recommended: yes, as before → after with the moved row first. Alternative: show only the moved item's old and new rank.
- **D3. Should a move that puts an item above the item it is blocked by be allowed?** Recommended: allow it with a warning in the preview. Alternative: refuse the move and say why.
- **D4. Is a Move sheet (Top, Up one, Down one, Bottom, Above or below…) the right touch fallback?** Recommended: yes, no drag on the app. Alternative: a drag handle on each row, as on the mobile web backlog.
- **D5. When a spoken name matches several items, should the phone ask?** Recommended: ask, with numbered choices to tap or say. Alternative: pick the best match and show it in the preview, where it can be removed.
- **D6. Should Needs me on the Features level include questions on their Stories?** Recommended: yes, rolled up to the Feature. Alternative: one mixed list of Features and Stories in waiting order.
- **D7. Where does a Story captured by voice land when no position is said?** Recommended: the bottom of the backlog, in the New state, not in a sprint. Alternative: the top, so it gets looked at.
- **D8. Should rank numbers show on every row?** Recommended: yes, they make voice moves ("to 2") and the preview easy to follow. Alternative: no numbers, order only.

## Decided (Joel, 8 Nov 2026, Lavish review)

- D1: recommended.
- D2: recommended.
- D3: recommended.
- D4: recommended.
- D5: recommended.
- D6: recommended.
- D7: alternative: a Story captured by voice lands at the **top** of the backlog (New, not in a sprint) so it gets looked at; "at the bottom" overrides. The `capture` state shows it.
- D8: recommended.

## Open questions

- Should one command move several items ("put SHIP-906 and SHIP-892 at the top")?
- Should the Stories level group by sprint, or stay one rank-ordered list?
- Does rank have to be within one Epic, or across the whole project as on the web?
- Who can reorder from the phone: any project member, or only the product owner?
- How should the Epics level look: the same rows with Feature counts, or something closer to a roadmap?
