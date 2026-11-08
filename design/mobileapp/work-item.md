# Work item (phone app)

Mock-ups: `work-item.html` (`?state=<id>`). Example: Feature SHIP-906 *Voice commands on the mobile app with on-device models* and its Story SHIP-921.

## Purpose

Open one Epic, Feature or Story to see where it stands, answer whatever waits on you, and leave notes, mostly by voice. It is not the web form: you can't edit the description or acceptance criteria here, and you can't move tasks.

## What's on screen

Top to bottom on a Feature:

1. **Header**: type, parent Epic, title, state, health, owner. Under it, a compact progress bar with one line of counts ("1 of 6 stories done · tasks 7/15 · 1 blocked"), then the **status line** with who verified it and when, and a **Still true** link.
2. **Needs you**: decisions to confirm (Approve / Discuss) and agents' "Needs input" questions (Reply). Each card shows the phrase to say. This is the main thing on the screen when something is waiting.
3. **Progress**: the full bar with a legend (done / in the current sprint / not planned) and the Stories and Tasks counts. Tasks only appear as counts.
4. **Goal and details**: the goal, cut to three lines, plus counts of links and files. Tap to read it all.
5. **Notes**: one timeline of notes and changes, newest first, filtered by All / Notes / Changes. Voice notes show the transcript first and the clip under it. Agents are marked with an Agent tag.

The dock at the bottom holds the hold-to-talk button and a typed fallback ("Type a note").

A **Story** shows the same header, then the tasks as counts with only the blocked task named ("Check model files against checksums · Sam · blocked 1d"), acceptance criteria (read-only), and Notes. A line says tasks are moved on the web task board.

## States

| id | What it shows |
|---|---|
| `feature` | Top of SHIP-906: header, status line, then Needs you (D10, the agent's question). |
| `notes` | Scrolled to Notes: agent note with a file, change events, voice notes with clips. |
| `listening` | Dictating "Set this to Active, approve D10, and add a note: …"; keys and states shown as hotwords. |
| `preview` | Three changes: state New → Active, approve D10, the note copied word for word with its 6 s clip. |
| `applied` | The new note and the two change events at the top of Notes, highlighted; top bar shows Active; Undo toast. |
| `reply` | Replying to Claude Code's "Needs input" question by voice; the reply is quoted word for word. |
| `story` | SHIP-921: tasks as counts (3 of 7 done, 2 in progress, 1 to do, 1 blocked) with the blocked task named. |
| `ask` | "What's left on this?" answered in a sheet from this item's data; Read aloud, nothing to apply. |
| `downloading` | First run: a banner with download progress, the mic off, the "Or say" hints hidden. Typing and buttons still work. |

## Voice actions on this screen

The model gets the item's key, type, its valid states, its open decisions and questions, its children's keys and titles, and the project's people.

| Action | What it does | Example |
|---|---|---|
| `add_note` | Posts a note. The text is copied from the transcript, never reworded, and the clip is attached. | "Add a note: the S22 numbers are fine for v1" |
| `set_state` | Changes the state to one valid for this type. | "Set this to Active" |
| `approve_decision` | Confirms an open decision (Dn). | "Approve D10" |
| `discuss_decision` | Leaves a word-for-word reply on a decision without confirming it. | "On D11: put it in the dock on every screen" |
| `reply` | Answers a "Needs input" question word for word, posted on the item; clears Needs input. | "Reply: try FunctionGemma on the S22 for one day" |
| `confirm_status` | Marks the status line as still true (updates Verified). | "Still true" |
| `set_status` | Replaces the status line with the words said. | "Status: Android download works, iPhone next" |
| `assign` | Sets the owner to a person on the project. | "Give this to Priya" |
| `ask` | Answers a question from this item's data, read-only. | "What's left on this?" |
| `open` | Opens a child, parent or linked item. | "Open SHIP-921" |

One utterance can carry several actions (state + decision + note); the preview lists each one, and any of them can be removed before applying.

## Left out compared with the web

- Editing the title, description, goal, acceptance criteria, tags, points, sprint or links. View only.
- Tasks: no list to edit, no moving tasks between states, no task board. Counts only, plus the blocked task named.
- Attachments: view and play only; no upload from the phone in v1.
- History beyond the change events in Notes; revision diffs.
- Retrospective logging on the item.

## Decisions for Joel

- **D1. Should Needs you come straight after the status line on a Feature?** Recommended: yes, so what waits on you is the first card, with a compact progress line in the header. Alternative: the full Progress card first, with Needs you below it.
- **D2. Should the phone offer Still true on the status line?** Recommended: yes, as a link and as the voice command "still true". Alternative: the status line is read-only on the phone.
- **D3. Should notes and changes share one timeline?** Recommended: yes, with All / Notes / Changes filters. Alternative: notes only, with changes left to the web history.
- **D4. Should a voice note keep its audio clip?** Recommended: yes, the transcript first and the clip under it for checking what was said. Alternative: keep only the transcript and drop the audio after posting.
- **D5. Can one voice command make several changes at once?** Recommended: yes, previewed as separate rows that can each be removed. Alternative: one change per command.
- **D6. Does approving a decision by voice need the preview like every other command?** Recommended: yes, the same preview, Apply or "yes", then Undo. Alternative: apply straight away and rely on Undo.
- **D7. Should replying to an agent's question by voice also hand the item back to the agent?** Recommended: yes, one step, and the preview says so. Alternative: post the reply, then hand back as a separate step.
- **D8. Should a Story show its tasks as counts only, naming just the blocked ones?** Recommended: yes. Alternative: a read-only list of every task.
- **D9. Should answers to questions appear on screen, with Read aloud as an option?** Recommended: yes, on screen first (this is SHIP-906 Q7). Alternative: speak the answer by default.
- **D10. While the models download, should the "Or say …" hints be hidden?** Recommended: hide them and grey out the mic, with a banner showing progress. Alternative: show the hints disabled so people learn the phrases early.

## Decided (Joel, 8 Nov 2026, Lavish review)

- D1: recommended.
- D2: recommended.
- D3: recommended.
- D4: recommended.
- D5: recommended.
- D6: recommended.
- D7: recommended, clarified: agents don't stay running to wait for an answer; they finish with a summary. The reply is posted on the work item as the answer, clears Needs input, and whichever agent picks the item up next reads it. The `reply` state says so.
- D8: recommended.
- D9: recommended.
- D10: recommended.

## Open questions

- Can you edit or delete your own note on the phone, for example to fix a misheard word?
- Should the state change pick from all states or only the next likely ones (New → Active, Active → Resolved)?
- Where does "6 stories ›" go: a children list on this screen, or the Backlog filtered to SHIP-906?
- Should Discuss on a decision open a reply sheet (typed or spoken), or just focus the dock?
- How long do voice clips live, and who can play them?
- Do people get a push notification when an agent sets Needs input on something they own?
