# Native app mock-ups (SHIP-916, epic SHIP-905)

First design mock-ups for the ShipLean phone app (epic SHIP-905). The app is a subset of the web app, built around **voice**: notes, keeping an eye on progress, giving feedback and approvals where people or agents wait on a human, and changing priority. Fine-grained editing (moving tasks between states, task boards) stays on the web; tasks show up only as aggregated status.

These are throwaway HTML mock-ups, not the Expo app. They use `../tokens.css`, so colours and fonts match the web app.

| File | What it is |
|---|---|
| `app.css`, `app.js` | The kit: phone frame (390 × 844), top bar, tab bar with the hold-to-talk button, detail-screen voice dock, rows, state pills, progress, ask cards, and the voice sheets (listening, preview, undo toast, voice notes). |
| `work-item.html`, `backlog.html`, `feature-board.html`, `retro.html`, `setup.html` | One file per area; each declares its states with `App.screen`. `setup.html` is first run (sign in, choosing the voice models, the download) and Settings › Voice. |
| `projects.html` | Projects (the app's root, SHIP-907 D7), the project key tile on every tab root, switching by tap or voice, Account, and no or lost projects. Decisions D1–D11 in `projects.md`; review page `projects-review.html`. |
| `typed.html` | The typed fallback: what text typed into the dock does. A Note \| Command switch shows the route before sending; notes post straight away, commands go through the voice preview. Decisions D1–D10 (all recommended, 9 Nov 2026) in `typed.md`. |
| `search.html` | Search (SHIP-928): the web's quick search popup (Ctrl/⌘+P) on the phone, with the same results, and the ways to reach it fast: the icon, holding the mic and sliding up, asking “where's…?”, the app icon's quick action, and Back Tap, the Action button or Siri. Decisions D1–D12 in `search.md`. |
| `settings.html` | Account (the sheet from the avatar on Projects) and the Settings screen it leads to: Voice, Notifications, Shortcuts, Appearance, About, Sign out. Decisions D1–D9 in `settings.md` (answered 9 Nov 2026, all recommended). |
| `<area>.md` | Design notes per area: what's on the screen, the voice commands it offers, decisions to confirm. |
| `index.html` | The review page: every state of every area side by side. |
| `screenshots.sh` | Regenerates `images/<area>-<state>.<theme>.png` with headless Chrome. |
| `voice.html` | Voice failures: nothing heard, nothing matched, no valid action, the Fix flow, Apply failing on the network, and Undo after it applies (22 states, on the work item and Backlog). Rules and decisions D1–D22 in `voice.md` (Joel, 9 Nov 2026: all recommended). |

Open a state directly: `work-item.html?state=preview&theme=dark`. `?list` prints a file's state ids.

## Shared voice model (from SHIP-906)

- **Hold to talk**, no wake word. The mic sits in the middle of the tab bar on list screens and in a dock on detail screens.
- While held, a **listening sheet** shows what the model will act on (the screen's context), the live transcript, and keys, names and states matched against the screen's words in the accent colour.
- On release, a **preview** lists each change the command will make. Notes are quoted word for word from the transcript. Nothing changes until **Apply** or “yes”; each change can be removed, and **Fix** re-records.
- After applying, an **undo toast**.
- When it goes wrong, see `voice.md`: a toast for silence, a sheet only when there's a choice, the heard words always kept, Apply all or nothing, offline changes wait to send, and Undo for 10 minutes on the voice change row.
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

## Projects and the tab bar (SHIP-925; Joel, 9 Nov 2026, Lavish review: all recommended)

- **Projects is the root of the app.** A project's tabs open on top of it. Every tab root (Inbox, Backlog, Features, Retro) shows the project's key tile at the top left (`App.tabRoot`), with the project name as the subtitle. Tapping the tile, or swiping back, returns to Projects. Detail screens keep their back button.
- **Account** is only on Projects (the avatar at the top right), together with Sign out and the way into Settings (`settings.md`: Account is you on the server, Settings is this phone).
- **After setup:** *Use ShipLean while this downloads* leads to Projects, or straight in with one project. A project you've never opened starts on Inbox. Later launches reopen the last project on the tab you left.
- **Switching** keeps the tab you're on. "Switch to Payments" works by voice from any tab, with no preview (no data changes), only a toast with Back. A dot on the tile means something new waits on you in another project.
