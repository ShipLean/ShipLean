# Mic tuning (phone app)

Mock-ups: `mic-tuning.html` (`?state=<id>`). Feature **SHIP-1374** (under SHIP-905). This replaces the Mic check sheet (`voice.html` `mic-check`, voice.md D3). The examples use the Android phone from `setup.md`.

## Purpose

On 12 Nov 2026 Joel held the mic three times on his phone, and every hold came back as *Didn't hear anything*. Then the Mic check sheet opened. The sheet shows a live level, but it can't say *why* nothing was heard, and it can't change anything. Joel asked for two things:

1. **Speakerphone by default.** Voice should always start on the phone's own microphone and loudspeaker, not on the earpiece or a connected headset.
2. **A Mic tuning page** to take people to, instead of the sheet.

## What the code does today (main 593899e0)

- `speechPresent.ts` treats a hold as speech only when at least 150 ms of it is louder than a **fixed** floor of RMS 300 (about −40 dBFS). It must also be 12 dB over the room. A phone or input whose voice level stays under that floor *always* reads as silent. Nothing can change the floor.
- `speechRecognizer.ts` records through expo-audio's `AudioStream`. On Android it's hard-coded to `AudioSource.MIC`. On iOS it's `.record` / `.measurement`. It doesn't choose an input, and `inputName()` always returns null. So the Mic check never says which mic it is using.
- The app never calls `setAudioModeAsync`. The audio route is whatever the phone was already using (for example, earbuds that are connected).

So three silent holds could come from three different causes. The mic may give no sound at all (a mic switch, another app, a blocked route). The voice may be real but under the fixed floor. Or the wrong input may be listening. The page tells these apart.

## Rules

- **One page, one test.** A 10-second test has two steps: stay quiet for 3 s (this measures the room), then hold and say a phrase. The result names one cause and one next step.
- **No sound and too quiet are different results.** Pure digital silence (all samples near zero, not even room noise) means something on the phone is stopping the mic, and sensitivity can't fix that. A real but quiet voice means the line moves down.
- **The test sets the line.** The line where speech starts counting goes halfway (in dB) between the room and the voice, for this phone and this input. It replaces the fixed −40 dBFS floor, and it's kept on the phone.
- **Speak in, read out.** The page shows the words it heard. It never plays the recording back. Nothing is recorded to a file, as today.
- **The test phrase is never applied.** It uses a real key from the screen you came from, so it tests what voice is really used for. It's only transcribed.
- **Back goes back.** The page is pushed on top of the screen you were on. Back, or *Back to SHIP-n*, returns there with the draft and the dock as they were.

## States

| id | What it shows |
|---|---|
| `second-silent` | The second silent hold in a row: the usual toast, now with a **Tune mic** action |
| `start` | The third silent hold opens the page: the input card (Phone · speakerphone), a live meter with the line, **Start the test**, **Type instead** |
| `step-quiet` | Step 1 of 2: stay quiet, a countdown, the room level in grey |
| `step-speak` | Step 2 of 2: hold the big mic and read the phrase; words appear as they're heard |
| `good` | Working: what was heard, the room and the voice on the bar, the new line, Sensitivity Automatic 7 of 10 |
| `no-signal` | No sound at all: a flat meter and three causes to check in order (Android: Microphone access in quick settings, another app, a covered mic) |
| `too-quiet` | Heard you, quietly: the old line was above your peak, which is why every hold said silent; the line moves down |
| `noisy` | Too noisy: the room is almost as loud as the voice, so closer and away from the noise |
| `input-sheet` | Change: Phone · speakerphone (default), connected Bluetooth, wired (greyed when not connected) |
| `adjust` | Adjust by hand: an Automatic switch and a slider that moves the line on the live meter |
| `settings` | Settings › Voice › **Mic tuning** row: input, when it was last tested, Automatic |

## Decisions

**Decided 12 Nov 2026 (Joel, in the Lavish review): all ten recommended.** Also recorded on SHIP-1374.

| # | Question | Recommended | Alternatives |
|---|---|---|---|
| D1 | What does the third silent hold in a row do? | Opens the Mic tuning page (full screen, pushed; Back returns) | Toast with a Tune mic action only; keep the Mic check sheet |
| D2 | Does the second silent hold offer it? | Yes, the toast's action is **Tune mic** | No, only the third |
| D3 | Which input and output does voice start on? | The phone's own mic and loudspeaker (speakerphone), every time, even with a headset connected | Follow the phone's current route (today); prefer a connected headset |
| D4 | Is an input picked on the page remembered? | Yes, on this phone, until changed. If it disconnects, voice falls back to the phone | Only until the app closes |
| D5 | What sets the speech line? | The test: halfway between the room and the voice, kept per phone and per input; until a test runs, today's −40 dBFS floor | Keep the fixed floor; the page only diagnoses |
| D6 | What phrase does the test ask for? | "Move <a key from the screen you came from> to Done", never applied | A fixed phrase ("Testing one two three"); any words |
| D7 | Is no sound at all a separate result? | Yes, with the causes for this phone in order | One "not hearing you" result |
| D8 | Can the line be set by hand? | Yes, under Adjust by hand, with Automatic on by default | No manual control |
| D9 | Where else is the page? | Settings › Voice › Mic tuning | Only after silent holds |
| D10 | Does the page play the test back? | No. It shows the words heard (speak in, read out) | — |

## For the engineer

- **Speakerphone default (D3).** expo-audio 58's `AudioStream` exposes no input or route choice. On Android, use `AudioRecord.setPreferredDevice` with the built-in mic (`TYPE_BUILTIN_MIC`), and use `AudioManager.setCommunicationDevice(TYPE_BUILTIN_SPEAKER)` only if a communication mode is ever used. On iOS, use `.playAndRecord` with `.defaultToSpeaker`, plus `setPreferredInput(builtInMic)`. This needs a patch to expo-audio, or a small native module beside it. `AudioRecorder` already has `setPreferredInput`, which can serve as a pattern. The same change fills in `inputName()`.
- **No signal (D7)** is a hold where every 30 ms frame has RMS under about 2 (−84 dBFS). Real rooms are never that quiet. When Android's mic switch is off, the stream delivers zeros.
- **Line (D5)**: store `{ inputId, roomRms, voiceRms, lineRms, testedAt }` in the voice settings. `speechPresent(pcm, lineRms)` takes the line in place of `FLOOR_RMS`. The 12 dB-over-noise and 150 ms rules stay.
- **Measure (AGENTS.md).** Count silent holds per 100 holds, per week, before and after a test on the same phone. Count test results by kind (good, no-signal, too-quiet, noisy) per week. Compare *Type instead* taps from Mic tuning per 100 openings with the old Mic check's rate.

## Open questions

- On Joel's phone (12 Nov), did the Mic check's level bar move when he talked? If it moved, the cause is too quiet (D5 fixes it). If it stayed flat, it's no signal, or the wrong input (D3 and D7).
- Were earbuds or a headset connected at the time?
