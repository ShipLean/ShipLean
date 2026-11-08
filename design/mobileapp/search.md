# Search (phone app, SHIP-928)

Search is the screen behind the search icon at the top right of every tab root (Inbox, Backlog, Features, Retro). **It is the web's quick search popup (Ctrl/⌘+P, SHIP-143) on a phone**: the same `GET /api/search`, the same scope, the same results and the same empty state. There is no new backend and no new behaviour for someone who already knows the web popup (D5–D8). What the phone adds is how you get there, and voice.

Mock-ups: `search.html` (`?state=<id>`).

## What's on screen (as the web popup, `features/quickSearch`)

- **Search bar** in place of the top bar. It has the field ("Search work items, sprints and projects"), a clear button, a **mic in the field** (hold to say the search words), and Cancel to go back to the tab you came from.
- **Nothing typed**: **Recent** shows the last 5 items opened on this phone. **Actions** follow, as in the web's `actions.ts`: Create epic, feature, story, bug and action item in the current project, Go to Backlogs, and Toggle theme. The note reads "Recent items are kept on this phone".
- **Typed**: **Work items** (at most 8; the current project's first, then the server's order), then **Sprints**, then **Projects**, then the Actions whose name contains the words. Each item row shows its type, its title (the first exact occurrence of the words is marked), its parents' keys, its state and its key. A match outside the title adds the web's second line: *In the description …*, *In a comment …* or *In attachment repro.md …*. Closed items are included and look like any other row. The note is the web's: "Top 8 items · keep typing to narrow", or "All your projects · SHIP first".
- **Nothing matches**: "Nothing matches "…"" and "Searched titles, descriptions, comments and attachments in your projects", the web's text.
- **Tapping a result** opens the work item screen. Back returns to the results, with the words and the scroll position kept.
- **Phone only: spoken results are numbered**, so "open 2" works. Typed results aren't numbered.

## Getting to Search quickly (the phone's Ctrl/⌘+P)

| Where you are | Shortcut | Why |
|---|---|---|
| Any tab root | **Tap the search icon** (top right, the same place on every tab) | Always visible. |
| Anywhere with the mic | **Hold the mic and slide up onto Search**, then say the words | A deliberate voice route into search mode. Everything said there is **search words, never a command**, so a sentence starting with "search…" can't be misread. Sliding back down returns to command mode, and sliding left still cancels. |
| Anywhere with the mic | **Ask a question** with plain hold to talk: "where's the sign-in story?", "find the demo video one", "open SHIP-912" | The command model sorts sentences by meaning, not by their first word. A question about where something is becomes a search, which runs at once because nothing changes. A strip says *You asked "…", so this is a search*, and **Not a search** sends the same words to the usual command preview. |
| Outside the app | **Long-press the app icon** › Search (also Inbox, Voice note) | A home screen quick action on iOS and Android. |
| Outside the app | **One Search shortcut the system can run**: iPhone's Action button, **Back Tap** (double-tap the back of the phone), Siri ("Search ShipLean"), Android app shortcuts and Google Assistant | The physical "double click", done the way the phone allows. Settings › Shortcuts explains how to set it up. |

**Pulling a list down refreshes it**, as in most apps. It doesn't open Search (Joel, D1).

**Turned down:**
- **Double-pressing a volume button.** iOS gives the volume buttons to apps only for taking a photo, and App Review rejects other uses. Android allows it only while the app is open, and the first press still changes the volume. Back Tap gives the same physical double tap.
- **A wake word.** The voice model is hold to talk with no wake word (SHIP-906).
- **Shaking the phone.** On iPhone, shake means undo.
- **A Search tab.** The tab bar is full, and search is a jump, not a place you stay.
- **Pull down to search.** Pull down refreshes (D1).

Following the tenet "speak in, read out" (README): a spoken search is answered on screen, as a list to read.

## States

| id | What it shows |
|---|---|
| `start` | Search just opened, with the keyboard up: Recent, then Actions. |
| `typing` | Results while typing "sign in", with the keyboard still up. |
| `results` | The keyboard is down. 8 items (the description, comment and attachment second lines, closed items as normal rows, PAY-31 after SHIP's items), a sprint, and the "Top 8" note. |
| `tip` | The one-time tip after the first use of the mic: slide up to search, or just ask, or tap the icon. |
| `slide-up` | Holding the mic and sliding onto the Search target. The listening sheet is in Search mode. |
| `voice-results` | Results for the spoken words, numbered, with "open 1" suggested. |
| `asked` | Plain hold to talk with "where's the story for signing in on the phone?": a search, with the *Not a search* strip. |
| `not-search` | After tapping Not a search: the same words in the command preview, as a note. |
| `empty` | The web's "Nothing matches" text. |
| `offline` | "Search needs a connection", with Try again. Recent stays below, because it's kept on the phone. |
| `home-shortcut` | Long-pressing the app icon: Search, Inbox (15), Voice note. |
| `settings` | Settings › Shortcuts: switches for the two voice routes, the note that pull down refreshes, and how to set up the outside-the-app ones. |

## Voice in Search

- The **mic in the field** and the **slide-up** route both take search words only. They never change anything, so there's no preview.
- On spoken results, "open 2" opens that row. Anything else is taken as new search words.

## Decisions (Joel, 9 Nov 2026, Lavish review)

- **D1. Which in-app shortcuts open Search?** The icon on every tab root, and holding the mic and sliding up onto Search, with D3 as well, so there are several ways. **Not pull down**: pulling a list down refreshes it, like most apps.
- **D2. In slide-up search mode, is everything said taken as search words?** Yes, as recommended.
- **D3. With plain hold to talk, do questions like "where's…?" and "find…" become a search?** Yes, by meaning, with no preview and a *Not a search* escape, as recommended.
- **D4. Does "open SHIP-912" open the item straight away?** Yes, as recommended. In search mode, a spoken key lists that item first instead.
- **D5. What does Search cover?** The same scope and results as the web's search popup. No new backend, and no change in behaviour for someone who already knows the web.
- **D6. Are closed items included?** As the web popup: included, and shown like any other row.
- **D7. What shows before anything is typed?** As the web popup: Recent (the last 5 items opened on this phone), then Actions.
- **D8. What does an empty result offer?** As the web popup: "Nothing matches" and what was searched.
- **D9. Offline search?** The alternative: Search says it needs a connection.
- **D10. Which outside-the-app shortcuts does the app ship?** A home screen quick action (Search, Inbox, Voice note), plus one Search shortcut that the system can run (Action button, Back Tap, Siri, Android app shortcuts, Assistant), as recommended.
- **D11. Can the in-app shortcuts be turned off?** Yes, each one in Settings › Shortcuts, all on by default, as recommended.
- **D12. When is the slide-up gesture taught?** A one-time tip after the first use of the mic, as recommended.

## Open questions

- The web's Actions include Go to Boards and Go to Sprints, but the app has no Boards or Sprints screens. The mock-up leaves both out. Should they open the Features tab and the web, or stay out?
- Should work items go into the phone's own search (Spotlight, Android search)? This copies titles onto the phone's index.
- On a detail screen (no tab bar), does the dock's mic get the same slide-up to Search?
