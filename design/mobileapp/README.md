# Native app mock-ups (SHIP-916, epic SHIP-905)

First design mock-ups for the ShipLean phone app (epic SHIP-905). The app is a subset of the web app, built around **voice**: notes, keeping an eye on progress, giving feedback and approvals where people or agents wait on a human, and changing priority. Fine-grained editing (moving tasks between states, task boards) stays on the web; tasks show up only as aggregated status.

These are throwaway HTML mock-ups, not the Expo app. They use `../tokens.css`, so colours and fonts match the web app.

| File | What it is |
|---|---|
| `app.css`, `app.js` | The kit: phone frame (390 × 844), top bar, tab bar with the hold-to-talk button, detail-screen voice dock, rows, state pills, progress, ask cards, and the voice sheets (listening, preview, undo toast, voice notes). |
| `work-item.html`, `backlog.html`, `feature-board.html`, `retro.html` | One file per area; each declares its states with `App.screen`. |
| `<area>.md` | Design notes per area: what's on the screen, the voice commands it offers, decisions to confirm. |
| `index.html` | The review page: every state of every area side by side. |
| `screenshots.sh` | Regenerates `images/<area>-<state>.<theme>.png` with headless Chrome. |

Open a state directly: `work-item.html?state=preview&theme=dark`. `?list` prints a file's state ids.

## Shared voice model (from SHIP-906)

- **Hold to talk**, no wake word. The mic sits in the middle of the tab bar on list screens and in a dock on detail screens.
- While held, a **listening sheet** shows what the model will act on (the screen's context), the live transcript, and keys, names and states matched against the screen's words in the accent colour.
- On release, a **preview** lists each change the command will make. Notes are quoted word for word from the transcript. Nothing changes until **Apply** or “yes”; each change can be removed, and **Fix** re-records.
- After applying, an **undo toast**.
- Each screen offers a small set of voice actions (about 5–12), so a small on-device model is enough.

## Shared decisions (Joel, 8 Nov 2026, Lavish review: all recommended)

- S1: "Feature board" in the app is progress across all Features; one Feature's own board is a read-only drill-in.
- S2: a fifth tab, Inbox, holds everything waiting on you (not mocked up yet).
- S3: a voice note keeps its audio clip next to the transcript for 30 days, playable by project members.
- S4: answers to spoken questions show on screen (a sheet when short, a screen when long), with Read aloud by on-device text to speech.
- S5: Health (On track / At risk / Blocked) is a new Feature field, set by people and agents, shown on the web too.
- S6: Needs you on a Feature counts the questions and decisions on its stories, everywhere.
- S7: the app uses native sizes (44px tap targets, 15px body); SHIP-805 stays the rule for the mobile web.
- Agents never wait for an answer: anything asked of a person is answered on the work item, for the next agent that picks it up (Work item D7).
