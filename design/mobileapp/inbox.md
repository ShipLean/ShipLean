# Inbox (phone app)

Everything waiting on you in one list, the app's first tab: the decisions to confirm, the questions people and agents have asked you, notes that @mention you, and your stale status lines. Each one is answered from its row by a tap, typing or voice.

Mock-ups: `inbox.html` (`?state=<id>`). The data is the same as setup's first answer ("What needs me?"): 6 items with 15 asks. They are 10 decisions (8 on SHIP-907, 2 on SHIP-906), 3 questions (SHIP-892, SHIP-924, SHIP-906), a mention (SHIP-742) and a status line to check (SHIP-743). Setup's **Open Inbox** button lands on the `from-setup` state.

## What's on screen

- **Top bar**: the tab-root bar (project tile, "ShipLean · 15 asks on 6 items") with search.
- **Waiting on you / Answered**: a segmented control. Waiting and the tab badge count asks (15), not items (D11).
- **Rows**: one row per work item, longest waiting first (D2). A Story's row stays under its Feature's row, slightly indented (SHIP-924 under SHIP-907), and the pair is placed by its oldest ask (D3). Each row has:
  - the type, key and title, and how long it has waited (shown in the warn colour after 2 days);
  - the kind: **N decisions** (with their D-numbers), **Needs input**, **Mentioned you** or **Status line to check**;
  - the ask itself, who asked, and what waits on it ("SHIP-906 waits on this");
  - answer buttons right on the row: **Approve** / **Discuss** for one decision, **Review all N** / **Approve all N** for a batch, the question's own options (20 MB, 50 MB) plus **Reply** for a question, **Reply** / **Seen** for a mention, **Still true** / **New status** for a status line;
  - when the item has more asks, a "2 decisions too · D10, D11" line that opens them in place.
- **Tapping the row** (not a button) opens the work item screen, where Needs you comes first (Work item D1).

## How answering works from the list

| Ask | Tap | What happens |
|---|---|---|
| One decision | **Approve** | Confirms the recommended answer at once. An Undo toast shows, and the decision becomes "D10 approved · just now". The row stays while other asks on the item are open. |
| One decision | **Discuss** | Opens the reply sheet titled "Discuss D11". The reply is posted word for word and the decision stays open. |
| A batch (D1–D8) | **Review all 8** | A tall sheet lists every decision with its recommended answer chosen. **Change** shows both options, and **Add why** dictates a reason. Nothing is sent until **Confirm 8**. |
| A batch | **Approve all 8** | The same sheet, scrolled to the button. Approve all is never one tap. |
| A question with options | an option | Posts that option as the answer, with Undo. |
| Any question | **Reply** | The reply sheet: the question quoted, quick answers, where the answer goes, then type or hold to talk. A spoken reply gets the usual preview and is quoted word for word. **Apply** posts it on the item, clears Needs input, and the row leaves Waiting. |
| A mention | **Reply** / **Seen** | Reply uses the same sheet and posts a note that answers the person. Seen clears the mention without posting anything. |
| A stale status line | **Still true** / **New status** | Still true marks the line as checked today, with Undo (the Features tab's "still true"). New status opens the reply sheet as "status: …", previewed like the Features tab's status update. |

Every answer is posted on the work item. **Agents never wait**: the next agent to pick up the item reads the answer there, and the sheets say so. A person who asked (Priya, Sam) is notified.

Following the tenet "speak in, read out" (README), the Inbox never reads anything aloud. Voice is how you answer; the list is how you read.

## States

| id | What it shows |
|---|---|
| `list` | Waiting on you: 15 asks on 6 items, longest waiting first, answer buttons on every row. |
| `from-setup` | Arriving from setup's Open Inbox: the same 6 items, plus a one-time tip that names a voice command to try. |
| `loading` | First open with nothing saved on the phone: skeleton rows, an updating bar, no badge. |
| `offline` | The Inbox as last loaded, with a banner. A tapped answer shows "sends when you're online". Voice still works because the models are on the phone. |
| `expanded` | SHIP-906 opened in place: its question, D10 and D11, each with its own buttons. |
| `approved` | D10 approved with one tap: the decided line, the count drops to 14, Undo. |
| `review` | Review all 8 on SHIP-907: recommended answers chosen, D4 changed to the alternative, Confirm 8. |
| `reply` | The reply sheet for SHIP-924's question. |
| `reply-listening` | Holding the mic in the sheet. The question is the context, so no key needs saying. |
| `reply-preview` | The answer word for word. Apply posts it on SHIP-924. |
| `answered` | SHIP-924 has left Waiting, the badge shows 14, Undo. |
| `voice-listening` | Holding the tab-bar mic on the list: keys, D-numbers and names matched against the whole Inbox. |
| `voice-preview` | Two items in one command. Approving D1–D8 warns "you haven't opened them yet" and links to See each. |
| `answered-tab` | What you answered, and what happened next: an agent picked it up, a person replied, or no one has yet. |
| `empty` | All clear: nothing waits. Shows how many you answered today and how many agents have picked up, plus something to try. |
| `empty-new` | A new person who has never had anything waiting: the kinds of thing that land here, and a push opt-in. |

## Voice actions on this screen

The model gets every open ask in the Inbox: its key, title, kind, D-number, the options and recommended answers, and who asked. Inside the reply sheet, the context is just that one ask.

| Action | What it does | Example |
|---|---|---|
| `approve_decision` | Confirms one decision, a range or all of them on an item, with their recommended answers. | "Approve D1 to D8 on 907" |
| `choose_option` | Picks one of a question's or a decision's options. | "On 924, personal team" |
| `reply` | Answers a question or a mention word for word and clears it. | "Tell Priya: 50 MB, and trim anything longer…" |
| `discuss_decision` | Replies on a decision word for word without confirming it. | "On D11: put it in the dock everywhere" |
| `confirm_status` | Marks a stale status line as still true. | "Still true on 743" |
| `set_status` | Replaces a status line with the words said. | "Status on 743: backups run in a container from Thursday" |
| `seen` | Clears a mention without replying. | "Seen 742" |
| `ask` | Answers a question about the Inbox on screen, read-only. | "What's waited longest?" |
| `open` | Opens an item. | "Open 906" |

## Decisions for Joel

- **D1. Should the Inbox show one row per work item, with all its asks inside?** Recommended: yes, one row per item, showing the first ask and opening the rest in place. That keeps SHIP-907's 8 decisions to one row. Alternative: one row per ask.
- **D2. What order should the Inbox use?** Recommended: the Feature's backlog rank, with a Story's row under its Feature's row, so the most important work comes first. Alternative: longest waiting first.
- **D3. Should a Story's question get its own row instead of being folded into its Feature?** Recommended: its own row, indented under the Feature, because the answer is posted on the Story. The Feature's Needs you count still includes it (S6). Alternative: group every ask under a Feature heading.
- **D4. Should tapping Approve on a single decision apply it at once?** Recommended: yes, with Undo. A tap is explicit, while a voice approval still gets the preview (Work item D6). Alternative: every approval opens a preview.
- **D5. Should Approve all always open the review sheet?** Recommended: yes. Every decision is listed with its recommended answer chosen, and nothing is sent until Confirm N. Alternative: approve all with one tap and rely on Undo.
- **D6. Should Reply open a sheet for that one question?** Recommended: yes. The sheet has quick answers, typing and hold to talk, with the question as the context. Alternative: Reply opens the work item and focuses its dock.
- **D7. When an ask is answered, should its row leave Waiting at once?** Recommended: yes, into Answered, which shows what happened next (an agent picked it up, or the person replied). Alternative: keep the row greyed out until the next refresh.
- **D8. What lands in the Inbox?** Recommended: decisions and Needs input questions addressed to you, plus any on items you own that have no addressee. Alternative: also @mentions in notes, and "Still true?" for your stale status lines.
- **D9. Should there be a way to snooze an item?** Recommended: no snooze in v1, because hiding an ask hides work that waits on it. Alternative: Later, which hides an item for a day.
- **D10. Should the app have a "Go through my inbox" walk-through?** Recommended: yes. It reads each item aloud and takes spoken answers, one at a time, each previewed. Alternative: no walk-through in v1, only voice from the list.
- **D11. What should the tab badge count?** Recommended: items (4). Alternative: asks (13).
- **D12. What happens to answers given offline?** Recommended: the Inbox shows as last loaded, and taps and answers wait, marked "sends when you're online". Voice still works. Alternative: the Inbox is read-only offline.
- **D13. Should a new item send a push notification?** Recommended: yes, for asks addressed to you by name. Asks on items you own only update the badge. Alternative: badge only, no push in v1.

## Decided (Joel, 9 Nov 2026, Lavish review)

- D1: recommended.
- D2: alternative, longest waiting first. A Story's row still sits under its Feature's row (D3), and the pair is placed by its oldest ask.
- D3: recommended.
- D4: recommended.
- D5: recommended.
- D6: recommended.
- D7: recommended.
- D8: alternative. @mentions in notes and "Still true?" for your stale status lines (3 days, Feature board D5) land in the Inbox too. Mentions get Reply and Seen; status lines get Still true and New status.
- D9: recommended.
- D10: alternative, no walk-through. Joel's reason, now the app's tenet "speak in, read out" (README): reading is faster than listening and speaking is faster than typing, so the app takes voice in and puts text out. It never narrates the screen. The walk-through state was removed, and so were Read aloud and Read it out on the work item, Features and Retro answer sheets.
- D11: alternative. The badge and the Waiting count are asks (15), not items.
- D12: recommended.
- D13: recommended.

## Open questions

- Decisions are written in item Markdown and attachments today. The Inbox needs each ask as data: its kind, question, options, recommended answer, who asked and who it's for. Is that part of SHIP-907, or a Feature of its own (with an MCP tool for agents to raise asks)?
- Can you change an answer after Undo has gone? Perhaps a correction posted as a new note, with the old answer struck through.
- Should an agent picking up an item with an open ask skip it, or carry on with the recommended answers? Agents never wait, so this decides what "SHIP-906 waits on this" really means.
- Should there be a view of what you asked others ("Waiting on others")?
- Does a person who asked get the Inbox too, or is the Inbox only for asks from agents?

## Kit candidates

- `.irow`: the Inbox row (kind label, ask, from, inline answers). The Backlog's Needs me and the Features tab's Needs you could share it.
- The reply sheet (`.reply`): a question quoted, quick answers, where it goes, then type or hold to talk. The work item's Reply could reuse it.
- The tall review sheet (`.sheet.tall`, `.dec`, `.radio`): a batch of decisions with recommended answers.
