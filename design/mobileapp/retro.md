# Retro (app)

**Purpose.** This is the current sprint's retrospective in your pocket. You read what agents and people logged, add your own points by voice, +1 the ones you agree with, and settle what needs a person: turning points into action items with an owner, marking them Not needed, and confirming theme merges that agents propose. Recurring themes and Features with retros give the view over time.

States in `retro.html`: `retro`, `dictate`, `dictate-preview`, `agree`, `action-preview`, `decide`, `features`, `answer`.

## What's on screen

- **Top bar.** Sprint 25 retro, its dates and how many retros have been logged. Under it, Notes · To decide (n) · Features.
- **Notes.** Four category tiles in the web's fixed order: Went well, Not in the plan, Went badly, Can be improved. Each tile shows its count and how many points are left to decide. The picked category is listed below, grouped by theme with the largest first, with a Recurring chip. A point shows who wrote it (a person, or Claude Code with its lane), the work item it is about, the text word for word, a voice clip if it was spoken, +1 with supporters' faces, and its decision: **+ Action item / Not needed**, or the action item's key and state. Went well never needs a decision.
- **To decide.** Feature retros that agents compiled, with their undecided points, root causes and the raw failure log folded away. Theme merges proposed by an agent appear as ask cards (Merge / Keep apart).
- **Features.** Recurring themes as a six-sprint heat strip with their action item, then Features with retros. Each shows its category counts, "n to decide" and "No Feature retro yet" where that applies.

## Voice actions (Retro tab)

| # | Action | Example |
|---|---|---|
| 1 | Add a point; the model picks the category | "retro note: we should profile the small model first" |
| 2 | Add a point to a named category | "went badly: the S22 build took 40 minutes" |
| 3 | +1 a point | "plus one that", "plus one the migration one" |
| 4 | Turn a point into an action item with an owner | "make that an action for Priya" |
| 5 | Add a point to an existing action item | "add that to SHIP-958" |
| 6 | Mark a point Not needed (the reason is kept word for word) | "not needed, SHIP-958 covers it" |
| 7 | Answer an agent's theme merge | "merge them", "keep them apart" |
| 8 | Show a category or view | "show went badly", "what's left to decide?" |
| 9 | Ask about the retro (answered in a sheet) | "what keeps going wrong?" |
| 10 | Read the current answer out | "read it out" |

Notes are always the transcript word for word, without the command words ("retro note:", "went badly:").

## Left out compared with the web

- By work item boards, the Coverage rail, and Insights' impact charts and verdicts.
- Search, renaming themes, Merge themes in full screen, and moving a point to another theme by hand.
- Archiving points, and editing an agent's text (it is read-only on the web too). The action item dialog's fix type and story points. Picking another sprint lives behind ⋯.

## Decisions for Joel

- **D1.** Should Notes show one category at a time, picked by four tiles, as on the mobile web (SHIP-727 D1)? *Recommended:* yes. *Alternative:* one list of all four categories, with the tiles as jump links.
- **D2.** Should Notes open on the first category with points to decide? *Recommended:* yes, Went badly in the mock-up. *Alternative:* always open on Went well, as on the web.
- **D3.** Should points get +1 (supporters stored per point)? This is new: the web has no voting. *Recommended:* yes. *Alternative:* no +1, and agreement is a note.
- **D4.** Should +1 only sort points within a theme and show faces, never deciding anything or feeding Recurring? *Recommended:* yes. *Alternative:* +1s weight Recurring and suggest action items.
- **D5.** When no category is said, should the model pick one and the preview offer a one-tap change? *Recommended:* yes. *Alternative:* always ask which category.
- **D6.** Should a dictated point need a start phrase such as "retro note:" or a category name? *Recommended:* yes, so commands and notes can't be confused. *Alternative:* any speech that isn't a command becomes a note.
- **D7.** Should an action item made by voice take the point's text word for word as its title? *Recommended:* yes, following the voice rule. *Alternative:* the model drafts a short title, marked "drafted", for you to edit.
- **D8.** Should "that" mean the point you last touched (tapped or +1'd), highlighted? *Recommended:* yes, with the model asking when nothing was touched. *Alternative:* the point at the top of the screen.
- **D9.** Can people +1 an agent's point? *Recommended:* yes. *Alternative:* +1 only on people's points.
- **D10.** Should theme merges proposed by agents be answerable in the app's To decide view? *Recommended:* yes. *Alternative:* web only.
- **D11.** Should a spoken point keep its audio clip next to the transcript? *Recommended:* yes, for 30 days. *Alternative:* keep the transcript only.
- **D12.** Should the Retro tab have a Features view with recurring themes? *Recommended:* yes. *Alternative:* the current sprint only, and over time stays on the web.

## Decided (Joel, 8 Nov 2026, Lavish review)

- D1: recommended.
- D2: recommended.
- D3: recommended.
- D4: recommended.
- D5: recommended.
- D6: recommended.
- D7: recommended.
- D8: recommended.
- D9: recommended.
- D10: recommended.
- D11: recommended.
- D12: recommended.

## Open questions

- A point dictated on the phone goes into your own retro. When it names a key ("before SHIP-906 gets…"), should About be that item rather than the sprint? The mock-up says yes.
- When the sprint has ended, should the tab show the last sprint's retro until the next one has its first point?
- Should agents be able to +1? This mock-up says no.
- "Not needed" by voice needs a reason. Is "not needed" alone enough, with the reason left empty?

## Kit candidates (page-local CSS in `retro.html`)

- `.cats` / `.cat`: category tiles. The `--cat-*` colours match `../screens/retro.css`.
- `.th` (theme header) and `.recurring` chip.
- `.rnote`: a retro point with an agent chip, `.foot`, `.agree` (+1), `.voters` and `.decided`. `.sel` marks the point "that" refers to.
- `.agent-chip`: Claude Code's lane or role.
- `.alt-pick`: a category picker inside a voice preview. It is spliced in before `.go`, because `App.preview` has no slot for extra content. A `below` slot on `preview` would help.
- `.heat` / `.heat-ax` (six-sprint strip), `.catbar` (category counts bar), `.legend`, `.no-fr`.
- `.needs` and the `.answer` sheet are shared with `feature-board.html`.
