# Typed fallback (phone app)

The dock on every detail screen has a mic and a **Type a note** field (work-item.md), and the voice failure sheets offer **Type instead** (voice.md on PR #89). No mock-up said what happens to typed text: does it post straight away, like the web's Comment box, or go through the preview, like a voice command? This area answers that on the SHIP-906 work item.

Mock-ups: `typed.html` (`?state=<id>`).

## Rules

- **The route shows before you send.** The open field always has two things: a **Note | Command** switch and a line under the field that says what Send will do. Send's label matches: **Post**, **Preview** or **Reply**.
- **The preview is only for words the model reads.** A typed note isn't interpreted, so it posts straight away, word for word, the way the web's Comment box does ("comments post straight away, not with Save"). A typed command is read by the same on-device model as speech, so it gets the same preview.
- **Where you came from picks the mode.** The dock opens on Note. *Type instead* on a voice failure sheet opens on Command, because a command was what you meant to say. *Reply* on a Needs input card opens a reply aimed at that question.
- **Nothing is lost.** Closing the keyboard keeps the text as a draft in the dock. Offline, a note waits to send, the same way voice changes and Inbox answers do.
- **Speak in, read out.** Typing is the fallback and the screen does the talking. Nothing is read aloud.

## States

| id | Group | What it shows |
|---|---|---|
| `note-typing` | Notes post straight away | The dock opened on Note. "Posts when you send, word for word, like a comment on the web" and **Post**. @ suggests people above the keyboard, as on the web. |
| `note-posted` | Notes post straight away | Posted with no preview. The note goes to the top of Notes, and an **Undo** toast shows for 8 s, the same as after a voice change. |
| `note-nudge` | Notes post straight away | In Note, "approve D10 and set this to active". A nudge says "Looks like 2 changes" and offers **Preview instead**. If it's ignored, Post posts the words as a note. |
| `command-typing` | Commands go through the preview | Command is picked: "Preview first · nothing changes until you apply" and **Preview**. Keys, states and names light up as they match, as in the listening sheet. |
| `command-preview` | Commands go through the preview | The voice preview, with **You typed** where the heard quote goes. **Edit** reopens the field with the text, and takes Fix's place. There's no clip. |
| `command-no-change` | Commands go through the preview | Sent as Command, but there's no change in it. It offers **Post as a note**, word for word (voice D9). |
| `type-instead` | Where you came from | From *Type instead* on the mic-blocked or mic-check sheet: the field opens on Command with "Microphone is off · typing instead". |
| `reply-typing` | Where you came from | **Reply** on Claude Code's question: the field is aimed at it, with no Note/Command switch. Reply posts straight away and hands the item back (work item D7). |
| `offline-note` | Edge cases | Offline: the note shows *Waiting to send* and sends by itself. |
| `downloading` | Edge cases | Voice models still downloading: Note works, Command is greyed out with "Command needs the voice models · ready in about 3 min". |
| `draft` | Edge cases | The keyboard is closed with text in the field. The dock shows the draft with its mode ("Note draft"). |
| `alt-auto` | Alternatives | A: no switch. After a pause the model reads the text, and Send turns from Post into **Preview 2**. Send waits until it has read the text. |
| `alt-always` | Alternatives | B: every send previews, even a plain note (one row, then Apply). |
| `alt-note-only` | Alternatives | C: typing only ever posts a note. Changes are made by voice or with the screen's buttons, and *Type instead* goes away. |

## Decisions (Joel, 9 Nov 2026, Lavish review: all 10 recommended)

Each decision takes its **Recommended** answer, and the mock-ups show it. The alternatives are kept for the record; `alt-auto`, `alt-always` and `alt-note-only` stay in typed.html as the drawn alternatives to D1.


- **D1. How typed text is routed.** Recommended: a **Note | Command** switch on the field, with the route shown before sending. Note posts, Command previews. *Alternative A:* no switch, and the model reads the text after a pause and relabels Send (Post / Preview 2). *Alternative B:* everything typed is previewed. *Alternative C:* typing makes notes only, and changes are made by voice or buttons.
  - *Why not A:* the route isn't known until the model has run (about 1–2 s on text, longer on a warm phone), so Send has to wait, or a quick send posts "approve D10" as a note. It also runs the model on every pause, which warms the phone more (the S22 note on SHIP-906). *Why not B:* it adds a tap to the most common typed case and goes against web parity, where comments post straight away. *Why not C:* when the mic is blocked, *Type instead* is the only way to make a change in one step.
- **D2. Does a typed note get a preview?** Recommended: no. It posts straight away and shows the 8 s Undo toast. *Alternative:* a one-row preview, then Apply.
- **D3. Which mode the field opens in.** Recommended: Note from the dock, Command from *Type instead*, and Reply from a Needs input card. The switch remembers the last choice until you leave the screen. *Alternative:* always open on Note.
- **D4. Nudge when a note reads like a command.** Recommended: yes. It's matched on the phone against the screen's own words (open decision keys, the type's state names, project people) with no model call, and it never switches the mode by itself. *Alternative:* no nudge.
- **D5. The preview of a typed command.** Recommended: **You typed** in place of the heard quote, and **Edit** (back to the field) in place of the mic Fix. *Alternative:* keep Fix, so a typed command can be corrected by voice.
- **D6. A Command with no change in it.** Recommended: offer **Post as a note**, as voice D9 does. *Alternative:* say there's no change and go back to the field.
- **D7. A typed reply to a Needs input question.** Recommended: it posts straight away and hands the item back. The line under the field says so. *Alternative:* preview it like a spoken reply.
- **D8. While the voice models download.** Recommended: Note works, and Command is greyed out with the reason and time left. *Alternative:* hide the switch until the models are ready.
- **D9. Drafts.** Recommended: a draft is kept on this phone per item until it's sent or cleared, and the dock shows it with its mode. *Alternative:* closing the keyboard throws the text away.
- **D10. Offline.** Recommended: Post works offline, and the note waits to send (voice D16). Command still previews offline, because the model is on the phone, and Apply then waits the same way. *Alternative:* Post is off while offline.

Follow-up from D1: voice.html's mic-blocked dock says "Type a note or command". It should say "Type a note", like every other dock, because the switch now shows the mode.

## Measure

Each send is an analytics event: `typed_sent { mode: note | command | reply, from: dock | type_instead | ask_card, nudge: none | shown | taken }`. It's always read against the voice holds in the same week, as typed sends per 100 holds per week, so we can see whether typing stays the fallback. Also per week:
- nudges taken per 100 nudges shown. A low rate means the nudge is noise.
- Undo per 100 typed notes, next to Undo per 100 voice notes. If typed notes are undone about as often as voice ones, the missing preview isn't costing anything.
- Edit per 100 typed previews, next to Fix per 100 voice previews.
- `command-no-change` per 100 Command sends. A high rate means people use Command for notes, so the default mode is wrong.

## Open questions

- Should a typed note keep the *voice* tag off? The mock-ups show a plain note, like the web. A small "typed on phone" mark wasn't drawn.
- When the switch is on Command and the text starts with "add a note:", the preview shows one note row, which is the same result as Note with one more tap. Should the phone switch back to Note, or leave it alone?
- Should drafts sync across the web and phone? The mock-ups keep them on this phone only.
