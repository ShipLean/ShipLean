# Microphone refused (phone app)

Mock-ups: `microphone.html` (`?state=<id>`). The examples are an iPhone 15 (6 GB, Auto → Qwen3 1.7B, 1.7 GB) for iOS and a Galaxy S24 Ultra (12 GB, Auto → Qwen3 4B, 3.1 GB) for Android, as in `setup.md`.

## Purpose

`setup.md` asked: *if the microphone is refused, what does the mic do?* No state showed it. Voice failures settled one piece (voice.md D4: the mic shows crossed out, and holding it opens a sheet with *Open Settings*), on one screen and for one phone. This area covers the whole path: saying no at setup step 3, what the app looks like afterwards, how each phone lets you turn the microphone back on, and coming back with it on.

What the phones allow decides most of this:

- **iPhone asks once.** After *Don't Allow*, the app can't show the prompt again. The only way back is the Microphone switch on ShipLean's page in iPhone Settings, which the app can open directly.
- **Android asks twice.** After a first *Don't allow* the app can ask once more. After a second, it can only open ShipLean's app info, where the microphone is under Permissions. *Only this time* lasts until the app has been in the background for a while, and then the prompt can show again.
- **Android 12 and later also have a switch for the whole phone** in quick settings. When it's off, the permission still reads as allowed, and Android shows its own *Unblock device microphone?* prompt when an app starts recording.

## Rules

- **Never a dead button.** The mic always reacts. When the microphone is off it's crossed out and reads *Mic off*. A tap or a hold opens a sheet that says how to turn it on, with *Type instead* next to it.
- **The phone's prompt is used up only by a real choice.** *Not now* on our explainer doesn't open the phone's prompt, so it's still there the first time you hold the mic.
- **Say exactly where to tap.** Each sheet draws the phone's own rows and marks the one to change.
- **Saying no isn't an error.** No banner, no dot on the avatar and no repeated asks. The crossed-out mic and Settings › Voice show it. Everything else, typed commands included, works.
- **Speak in, read out.** Every message is on screen.

## What's on screen

- **Setup, step 3** (`setup.html` `microphone`): *Continue* opens the phone's own prompt with our purpose string: "Voice listens only while you hold the button. What you say is turned into text on this phone." Drawn for iPhone (`prompt-ios`) and Android (`prompt-android`).
- **Refused at setup** (`refused-setup`): setup goes on to the Downloading page. A card above the files reads *Microphone is off*: "Voice will be ready, but it can't hear you until the microphone is on. Typed commands still work." It has *Open Settings*. The download carries on.
- **Not now at setup** (`not-now`): the same page with a quieter dashed line: *Microphone: not asked yet. We'll ask the first time you hold the mic.*
- **The mic with the microphone off** (`mic-off`): crossed out, grey, labelled *Mic off*, on the tab bar and the dock alike (the dock is drawn in `voice.html` `mic-blocked`). No banner.
- **Turn it on, iPhone** (`sheet-ios`): *Turn on the microphone in Settings*. "iPhone won't ask again, so it's one switch in Settings." A drawing of Settings › ShipLean with the Microphone switch marked *Turn on*. *Type instead* and **Open Settings**, then "Everything else works without voice".
- **Ask again** (`sheet-ask`): when the phone can still ask (after *Not now* on either phone, or after one *Don't allow* on Android). *Allow the microphone to talk to ShipLean* with the three promises from the explainer. *Type instead* and **Allow microphone**, which opens the phone's prompt.
- **Turn it on, Android after a second no** (`sheet-android`): a drawing of app info › Permissions › Microphone with *Allow only while using the app* marked *Pick this*. *Type instead* and **App settings**.
- **Android's microphone switch is off** (`android-switch`): the permission is on, so the mic looks normal. Holding it brings up Android's own *Unblock device microphone?*. The app adds nothing of its own.
- **Back with the microphone on** (`back-on`): the mic returns, with a toast: *Microphone on · hold to talk*. iPhone may restart the app after a privacy setting changes, so the app reopens on the screen you left.
- **Voice ready, microphone off** (`ready-mic-off`): the one-time ready coach mark from `setup.html` `ready` changes. *Voice is ready. Turn on the microphone to use it.* "It's off in iPhone Settings, so voice can't hear you yet. Typed commands work now." *Not now* or **Open Settings**. The guided first command ("What needs me?") waits until the mic is first allowed.
- **Settings › Voice** (`settings-voice`): the top card reads *Voice is ready, but the microphone is off*. The Microphone row is marked **Off** ("Off in iPhone Settings. Tap to turn it on") and opens the same sheet as the mic. On the Settings screen (`settings.html`), the Voice row's value reads *Ready · mic off*.

## States

| id | What it shows |
|---|---|
| `prompt-ios` | Setup step 3: iPhone's one-time prompt over our explainer, with the purpose string. |
| `prompt-android` | Setup step 3: Android's prompt (While using the app, Only this time, Don't allow). |
| `refused-setup` | After Don't Allow: Downloading carries on, with the *Microphone is off* card and Open Settings. |
| `not-now` | After Not now: Downloading with *Microphone: not asked yet*. |
| `mic-off` | Backlog with voice ready and the mic crossed out, *Mic off*. No banner. |
| `sheet-ios` | Tap the mic: Settings › ShipLean › Microphone drawn, Open Settings, Type instead. |
| `sheet-ask` | The phone can still ask: the promises and Allow microphone. |
| `sheet-android` | Android after a second no: app info › Permissions › Microphone drawn, App settings. |
| `android-switch` | Android's phone-wide switch is off: Android's own Unblock prompt. |
| `back-on` | Returned with the switch on: the mic is back and a toast says so. |
| `ready-mic-off` | The models finished but the mic is off: the coach mark asks to turn it on. |
| `settings-voice` | Settings › Voice: the card and the Microphone row marked Off. |
| `alt-greyed` | D1 alternative: greyed mic labelled *Microphone off*, which does nothing. |
| `alt-setup-page` | D2 alternative: a refusal page in step 3 before Downloading. |

## Voice actions on these screens

None. The microphone is off, so the screens here are touch only. *Type instead* opens the composer on Command (typed.md D3), which uses the same command model as voice.

## Left out

- The phone's own Settings and app info pages: drawn small on the sheets, not as screens.
- **Restricted microphones** (Screen Time, or a work phone's device management): neither phone tells an app clearly that the switch is locked, so the normal sheet shows. See open questions.
- The mic being busy in a phone call or another app: a voice failure, not a permission, so it belongs in `voice.md`.
- iPad and Android tablets (SHIP-932).

## Decisions

Answered by Joel on 9 Nov 2026: all ten take the recommended option.

- **D1. What does the mic do when the microphone is refused?** **Decided (recommended):** crossed out and labelled *Mic off*. A tap or a hold opens a sheet that says how to turn it on, with *Type instead* (this extends voice.md D4 to taps and to the tab bar). Not chosen: greyed with a label, doing nothing, so turning it on is found only in Settings › Voice (`alt-greyed`).
- **D2. Where does setup go after Don't Allow?** **Decided (recommended):** on to the Downloading page, with a *Microphone is off* card and *Open Settings*. No extra step. Not chosen: a page of its own in step 3 with the steps, *Open Settings* and *Continue without the microphone* (`alt-setup-page`).
- **D3. Does Not now use up the phone's prompt?** **Decided (recommended):** no. *Not now* closes our explainer without opening the phone's prompt. The first hold of the mic then shows the short *Allow microphone* sheet, which leads to the prompt. Not chosen: *Continue* and *Not now* both open the phone's prompt, so the choice is made once at setup.
- **D4. Does the download carry on when the microphone is refused?** **Decided (recommended):** yes. Typed commands use the command model too, and if the mic is turned on later, voice works straight away. Not chosen: pause the download and ask *Download the voice models anyway?*.
- **D5. Android: ask again after the first no?** **Decided (recommended):** yes, once, from the sheet's *Allow microphone*, since Android allows a second prompt. After a second no, the sheet leads to app settings. Not chosen: always go to app settings after the first no, so the person is never asked twice.
- **D6. Draw the phone's rows on the sheet?** **Decided (recommended):** yes. A small drawing of the rows on the page that *Open Settings* opens, with the one to change marked. Not chosen: only the sentence and the button.
- **D7. The models are ready but the microphone is off?** **Decided (recommended):** the ready coach mark asks to turn the microphone on (*Not now* / *Open Settings*). The guided first command waits until the mic is first allowed. Not chosen: no coach mark, just a toast saying *Voice is ready*.
- **D8. Does a refused microphone count as "needs you"?** **Decided (recommended):** no. It shows in Settings › Voice (the card and the row marked Off) and on the Settings row, but there's no dot on the avatar, no banner and no notice at the top of Settings. The person chose this, and the crossed-out mic already shows it. Not chosen: treat it like blocked notifications: a dot on the avatar and a line in the Settings notice (settings.md).
- **D9. Coming back with the microphone on?** **Decided (recommended):** a toast, *Microphone on · hold to talk*, and the app reopens on the screen you left, even when iPhone restarted it. Not chosen: no toast. The mic just looks normal again.
- **D10. Android's phone-wide microphone switch?** **Decided (recommended):** leave it to Android's own *Unblock device microphone?* prompt and add nothing. If the person cancels, the hold ends as *Didn't hear anything* (voice.md). Not chosen: check the switch first and show our own sheet pointing to quick settings.

## Measure (AGENTS.md)

- `mic_permission { result: allowed | refused | not_now | only_this_time, where: setup | mic | settings, can_ask_again }`. Read as **setups with the microphone allowed per 100 setups that reached step 3, per week**, split by platform. This is the baseline the other numbers compare against.
- **Turned on later per 100 refused, within 7 days and within 30 days**, split by the way back (*Open Settings* from the sheet, the coach mark, Settings › Voice). If the sheet's drawing works (D6), most come from the sheet.
- **Type instead per 100 sheet opens, per week**, next to **typed sends per 100 voice holds** for people with the mic on (typed.md). This shows whether people with the mic off use typed commands or stop making changes on the phone.
- **Sheet opens per active day for people with the mic off.** A high number means the crossed-out mic is pressed by mistake and D1 is costing taps.

## Open questions

- Can the app tell when the microphone is locked by Screen Time or by device management, so the sheet could say so and drop *Open Settings*? If not, the normal sheet shows and the switch in Settings is greyed out.
- Does iPhone always restart the app when the microphone is turned **on** in Settings, or only when it's turned off? The mock-ups assume either can happen and restore the screen.
- Android resets the permissions of apps that haven't been used for months. Should the app say anything when that happens, or just show *Allow microphone* on the next hold, as the mock-ups do?
- After *Only this time* runs out on Android, the next hold opens the phone's prompt straight away, with no sheet of ours. Is that right, or should the short sheet come first?
