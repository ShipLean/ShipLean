# Voice failures (phone app)

Every other area draws only the success path: hold, listen, preview, Apply, undo toast. This area covers what happens when it goes wrong: **nothing heard, nothing matched, no valid action, the Fix (re-record) flow, Apply failing on the network, and Undo after it applies.** The failures are drawn on screens that already exist: the SHIP-906 work item (dock) from `work-item.html` and the Backlog (tab bar) from `backlog.html`.

Mock-ups: `voice.html` (`?state=<id>`).

## Rules every failure follows

- **Say what happened and that nothing changed.** Each failure before Apply ends with "nothing was changed". Each failure after Apply says what did and didn't go through.
- **Keep what was said.** A failure never throws the words away. The heard sentence stays on screen, and the words that didn't match are underlined.
- **Offer the next step.** That's Fix (hold and say it again), a one-tap correction (a close match, a valid state), or the place where it can be done (the web).
- **Speak in, read out.** Failures are shown, never spoken. No error sounds and no read-aloud (README tenet).
- **A small failure gets a small message.** A tap or silence shows a toast above the mic and nothing else. A sheet appears only when there's something to choose.

## States

| id | Group | What it shows |
|---|---|---|
| `too-short` | Nothing heard | A tap under half a second. Toast: "Hold the mic while you speak". |
| `nothing-heard` | Nothing heard | Held, but silent. The sheet closes. Toast: "Didn't hear anything · nothing was changed", and which mic was used. |
| `mic-check` | Nothing heard | The third silent hold in a row. Mic check: a live level meter, the input in use (AirPods Pro) with *Use iPhone mic*, *Type instead*. |
| `mic-blocked` | Nothing heard | Microphone permission is off. The dock's mic is crossed out, and holding it opens a sheet with *Open Settings*. |
| `no-match` | Nothing matched | "Set the onboarding story to Active": no item is called that. Search's two closest items, each one tap to use instead, plus Fix and Search. |
| `misheard-key` | Nothing matched | Backlog, "Move SHIP-960 up to second". There's no SHIP-960. The phone offers SHIP-906, which sounds the same and is on the list. *Yes, SHIP-906* goes on to the usual rank preview. |
| `web-only` | No valid action | "Move the checksum task to Done". Tasks move on the web, so the sheet says so, with *Open on the web*. |
| `bad-state` | No valid action | "Set this to Done". Features have no Done. Their states are offered as chips, and one tap opens the preview. |
| `unclear` | No valid action | Words, but no change in them. *Add as a note* (still previewed), Fix, *What can I say here?* |
| `partial` | No valid action | Three parts, one of them web only. The preview strikes that one out with the reason, and *Apply 2* does the rest. |
| `what-can-i-say` | No valid action | The screen's voice actions as a list to read, and what's web only. |
| `fix-hold` | Fix | Holding Fix on the preview. The preview folds to a numbered list. You say only the correction ("No, D11, not D10") or "start over". |
| `fix-preview` | Fix | Only the corrected row changes, marked *Fixed · was D10*. The heard text shows both sentences. |
| `fix-type` | Fix | Tapping a note's quote opens it for typing, here to correct a misheard "has 22". The note is marked edited and keeps its clip. |
| `apply-failed` | Apply failing | No answer from ShipLean. The preview stays with *Try again*, *Send when online* and *Discard*. Nothing was changed. |
| `apply-queued` | Apply failing | Offline. The changes show on the item as *Waiting to send*, the way the Inbox shows queued answers, and send by themselves. Undo still works. |
| `apply-conflict` | Apply failing | Priya resolved SHIP-906 a minute ago, so nothing was applied. The preview comes back with the state row now reading *Resolved → Active*, and you confirm again. |
| `send-failed` | Apply failing | Back online, the queued changes sent, but Claude Code had withdrawn D10. A banner on the item says which change didn't go through and why. |
| `undone` | Undo | Undo on the toast. All three go back at once, and History keeps one line. |
| `undo-later` | Undo | The toast is gone. The *Your voice change* row in Notes keeps Undo for 10 minutes, and the undo is previewed like any change. |
| `undo-partial` | Undo | Claude Code has picked up the item and read the D10 answer. Two changes undo, and D10 is changed as a decision instead. |
| `undo-failed` | Undo | Undo couldn't reach ShipLean. Nothing was undone, and *Try again* stays on the toast. |

## Decisions (Joel, 9 Nov 2026, Lavish review: all 22 recommended)

Each decision below takes its **Recommended** answer, and the mock-ups show that answer. The alternatives are kept for the record.

- **D1. How "nothing heard" is shown.** Recommended: a toast above the mic, with no sheet. Alternative: keep the listening sheet open with "Didn't catch that" and a Try again button.
- **D2. What a quick tap on the mic does.** Recommended: a hint, "Hold the mic while you speak". Alternative: a tap starts hands-free listening, and a second tap stops it.
- **D3. Repeated silence.** Recommended: the third silent hold in a row opens a mic check (a level meter and which input is in use). Alternative: the toast every time, with no mic check.
- **D4. Microphone permission off.** Recommended: the mic shows crossed out, and holding it explains, with *Open Settings*. Alternative: hide the mic until permission is given.
- **D5. Nothing matched.** Recommended: show search's two closest items, each one tap to run the same command on it. Alternative: say nothing matched and offer only Fix.
- **D6. A key that doesn't exist.** Recommended: offer one key that sounds the same, only when that item is on the screen. Alternative: never guess a key, and say only that there's no such item.
- **D7. Something only the web can do.** Recommended: say where it's done, with *Open on the web*. Alternative: only "can't do that here".
- **D8. A state the item doesn't have.** Recommended: offer the item type's states as chips, and a tap opens the preview. Alternative: an error and Fix.
- **D9. Words with no change in them.** Recommended: offer *Add as a note*, which still goes through the preview. Alternative: Fix only.
- **D10. A command with a part that can't be done.** Recommended: preview the parts that can be done, show the rest struck out with the reason, and *Apply 2*. Alternative: refuse the whole command.
- **D11. Help for voice.** Recommended: *What can I say here?*, from a link on the failure sheets or by saying it, lists the screen's actions. Alternative: one help page in Settings › Voice.
- **D12. What Fix does.** Recommended: hold Fix and say only the correction, which changes the preview; "start over" replaces it. Alternative: Fix always re-records the whole command.
- **D13. Correcting a note's words by typing.** Recommended: tap the quote to edit it. The note is marked edited and keeps its clip. Alternative: fixes by voice only.
- **D14. All or nothing.** Recommended: Apply sends every change in one request and the server applies all of them or none. Alternative: each change on its own, and the ones that failed are listed.
- **D15. When Apply fails.** Recommended: the preview stays open with *Try again*, *Send when online* and *Discard*. Alternative: queue the changes without asking.
- **D16. Offline.** Recommended: Apply works offline. The changes show on the item as *Waiting to send* and go by themselves, as the Inbox's answers do. Alternative: Apply is off while offline.
- **D17. The item changed since the preview.** Recommended: apply nothing, bring the preview back with the changed row updated and marked, and confirm again. Alternative: apply anyway, so the last write wins.
- **D18. A queued change refused later.** Recommended: a banner on the item and a push notification saying which change and why. The others stay applied. Alternative: the banner only, seen the next time the item is opened.
  - *How D14 and D18 fit together:* an Apply made while online is all or nothing, because you're there to confirm again. A batch that waited offline is sent with nobody watching, so each change goes through if it still can and the refused ones are reported (D18). Undo works the same way (D21).
- **D19. How long Undo lasts.** Recommended: the toast for 8 s, then Undo on the *Your voice change* row for 10 minutes, or until someone else changes the item. Alternative: the toast only.
- **D20. Undo after the toast.** Recommended: previewed like any change, with "yes" or Undo. Alternative: undo at once, with no preview.
- **D21. Undoing something already used.** Recommended: undo what can be undone, explain the rest, and offer *Change D10* (SHIP-922). Alternative: block the whole undo.
- **D22. What an undo leaves behind.** Recommended: one History line, "Joel undid a voice change", and the note is gone. Alternative: the note stays, shown as removed.

## Measure

Following AGENTS.md, each failure is an analytics event with its kind (`voice_failed { kind: nothing_heard | too_short | no_match | no_action | partial | apply_failed | conflict | send_failed | undo_failed }`), always read against the holds started in the same period: failures per 100 holds, per week, by kind. Fix and Undo are rates too: Fix per 100 previews, and Undo per 100 applies, per week. A rising Fix or Undo rate means the model is getting commands wrong, even when nothing "fails".

## Open questions

- A short vibration on a failure? It's on-device output but not narration. The mock-ups assume the phone's standard error buzz on nothing heard and nothing matched.
- When the toast is still showing, does "undo that" undo at once like the button, or preview?
- How long can changes wait to send while offline before the phone asks whether they still apply (a day?)
- Not drawn: a command someone isn't allowed to make (a Viewer in a project). It would follow `web-only`, with "You can't change items in PAY" as the reason.
