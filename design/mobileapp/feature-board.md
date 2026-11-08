# Feature board (app)

**Purpose.** This is where you keep an eye on progress across Features from the phone. You can see which ones are healthy, which are stuck and which are waiting on you, and you can answer, approve, add a note or make a status call by voice. On the web, "Features board" is the board of Features and "Feature board" is one Feature's taskboard. In the app, **Feature board** is the monitoring list (tab: Features), and one Feature's own board is a read-only drill-in.

States in `feature-board.html`: `board`, `needs-you`, `blocked-answer`, `move-listening`, `move-preview`, `move-applied`, `status-update`, `feature`, `digest`.

## What's on screen

- **Features tab.** Filter chips: All · Needs you · At risk · Blocked. Below them, a "changes since yesterday" banner, then Features as a stacked list grouped by state: Active, Resolved, New, and Closed folded into one line. Each row shows the type icon and key, health, a Needs you pill, the title, the **status line** with who wrote it and how old it is (warn colour after 3 days), the progress bar (done / current sprint / other sprint) and the story and bug roll-ups.
- **Needs you.** Every decision or question waiting on a person, grouped under its Feature, using the kit's ask cards. Each can be answered by a tap or by voice.
- **Expanded Feature** (after a voice command, or on tap). Shows the latest note, the open decision, the status history, and the Change state and Open board buttons.
- **One Feature's board** (`feature`). Read-only. It shows the state, health and status line, four figures (stories, bugs, tasks, points), the progress bar and a "Read-only. Tasks move on the web" link. Below that are stories and bugs grouped by state. Each one shows its sprint, owner, a Needs you pill and a done / doing / to do bar for its tasks. There are never task cards.
- **Digest** (`digest`). A long answer opens as a screen, not a sheet: Needs you first, then state and health changes, status lines and agent work. It has Read it out and Mark as seen.

## Voice actions (Features tab)

| # | Action | Example |
|---|---|---|
| 1 | Ask what's blocked or at risk (answered in a sheet) | "what's blocked?" |
| 2 | Ask what changed since a time (opens the digest) | "what changed since yesterday?" |
| 3 | Move a Feature to another state | "move SHIP-907 to Active" |
| 4 | Add a note to a Feature (word for word) | "note on SHIP-906: the S22 runs hot after a minute" |
| 5 | Set the status line (word for word) | "status on SHIP-743: waiting for Sam's host upgrade" |
| 6 | Set health | "mark SHIP-906 on track" |
| 7 | Answer a Needs input question | "answer SHIP-906: Parakeet, because it is faster" |
| 8 | Approve a decision | "approve D1 to D8 on SHIP-907" |
| 9 | Change priority (backlog rank) | "move SHIP-743 above SHIP-742" |
| 10 | Filter or open | "show what needs me", "open SHIP-906's board" |
| 11 | Read the current answer out | "read it out" |

When a Feature is open (expanded, or on its board), the key can be left out: "status: blocked on the sign-in decision". On the read-only board, actions 3 to 8 apply to that Feature, and questions about it are answered in a sheet ("how far is SHIP-914?").

## Left out compared with the web

- Moving, adding or reassigning tasks. Drag and drop. The Taskboard grid. Grouping by People. Card settings.
- WIP limits, the definition of done, lanes by Epic, and the Epics board. Editing a Feature's fields, tags or parent.
- Creating Features. That belongs to voice capture under Work item and Backlog.

## Decisions for Joel

- **D1.** Should the Features tab be a stacked list grouped by state? *Recommended:* yes, a stacked grouped list, because one scroll covers everything and it reads well with voice answers. *Alternative:* a sideways pager with one state per page, like the mobile web boards (SHIP-719).
- **D2.** Should health (On track / At risk / Blocked) be set explicitly by people and agents? *Recommended:* yes, set by hand, by voice or by an agent via MCP. *Alternative:* compute it from stale status lines, blocked stories and open asks.
- **D3.** Should the status line be a field of its own, changed only by "status: …"? *Recommended:* yes, so notes never overwrite it. *Alternative:* the latest note is the status line.
- **D4.** When a status update says "blocked", should the preview also propose the health change as a separate line you can remove? *Recommended:* yes. *Alternative:* never infer health from words.
- **D5.** Should a status line be flagged as stale after 3 days? *Recommended:* 3 days, fixed. *Alternative:* a per-project setting.
- **D6.** Should groups run Active, Resolved, New, then Closed folded to one line? *Recommended:* yes, because what moves comes first. *Alternative:* the board's column order (New first).
- **D7.** Can a Feature's state be changed in the app, by voice or a Change state sheet, without drag? *Recommended:* yes. *Alternative:* Features are read-only in the app too.
- **D8.** When a Feature with open stories is moved to Closed, should the preview warn ("3 stories still open") and still allow Apply? *Recommended:* warn and allow, matching the web's "Move to Done anyway". *Alternative:* refuse in the app and point to the web.
- **D9.** Should one Feature's board be in the app, read-only, with stories by state and task counts? *Recommended:* yes. *Alternative:* no drill-in, just Open on web.
- **D10.** Should short answers show in a sheet and long ones (the digest) open as a screen? *Recommended:* yes. *Alternative:* always a sheet.
- **D11.** Should Read it out use on-device text to speech in v1? *Recommended:* yes. *Alternative:* answers are on screen only in v1.
- **D12.** Should "since yesterday" in the banner mean since you last pressed Mark as seen? *Recommended:* since last seen. *Alternative:* a fixed 24 hours.

## Open questions

- Health and the status line are new fields on a Feature. Should the web's Features board show them too?
- Do Epics get a view in the app, or only a filter on this tab ("show SHIP-905")?
- Should Needs you on a Feature include asks on its stories? The mock-up counts them (SHIP-914 on the board).
- Change priority by voice works on backlog rank, but this list is grouped by state. Should a rank change show the new order in the preview?

## Kit candidates (page-local CSS in `feature-board.html`)

- `.row.frow`: a Feature row with health, status line, freshness (`.stale`) and progress.
- `.needs`: the "Needs you" pill (also used in `retro.html`).
- `.grp-h`: a group header with a state pill and count.
- `.answer` sheet with `.lead`, `.ans-list` and `.ans-row`: a voice answer with Read it out and no Apply (also in `retro.html`).
- `.figs` (roll-up figure tiles), `.srow` and `.tbar` (a story with its tasks aggregated), `.sprint` pill.
- `.chg` (digest change row), `.asked` ("You asked …" line).

**Kit issues found:**
- `act({ kind: 'state' })` puts class `state` on the act, which collides with the `.state` pill: the row gets a 22px height and a dot, and its content is clipped. I used `kind: 'field'` with `icon: 'flag'` instead.
- A long `ctx` in `preview` or `listening` wraps badly in `.voice-ctx`. It needs `min-width: 0` and an ellipsis.
- Keys in `.hot` and `.quote` break at the hyphen ("SHIP-" / "906"). I used a non-breaking hyphen.
