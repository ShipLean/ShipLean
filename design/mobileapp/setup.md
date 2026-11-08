# Install and setup (phone app)

Mock-ups: `setup.html` (`?state=<id>`). The examples are a Galaxy S24 Ultra (12 GB), a Galaxy S22 (8 GB) and an iPhone 15 (6 GB). Voice decisions come from SHIP-906 (`voice-commands-design.md`) and sign-in from SHIP-907 §4.

## Purpose

Get someone from a fresh install to a working app with voice switched on, without making them wait for the download. First run has three steps: **sign in**, **choose the voice models**, then **allow the microphone**. The download starts as soon as the models are chosen, and the person can use the rest of the app straight away. When both models have been downloaded and checked, a guided first command shows them how voice works. **Settings › Voice** is where they change the model, free space or read the licences later.

## What's on screen

- **Welcome**: the logo (a copy of `design/logo`), one line on what the app does, and three points: hold to talk, runs on this phone, what needs you comes first. Get started.
- **Sign in** (step 1 of 3): the Server field prefilled with `https://prod.shiplean.app`, the health check line ("ShipLean 1.9 · connected"), and a note that Sign in opens the browser and the app never asks for a password. Sign in is enabled only after the check passes. The browser confirm page and the error messages are as specified in SHIP-907 and aren't drawn again here.
- **Choose your voice models** (step 2 of 3), the key screen:
  - **Command model**: three choices. **Auto** (Recommended, selected) shows what it picked for this phone: "This phone: 12 GB → Qwen3 4B". **Qwen3 1.7B** (1.1 GB, tagged *Quicker*) and **Qwen3 4B** (2.5 GB, tagged *More accurate*) each give a time per command on this phone and say what the model is good at.
  - **Speech to text**: Parakeet TDT 0.6B (0.6 GB), with a tick and no choice, labelled "Same on every phone".
  - **Download summary**: the total for the current choice (3.1 or 1.7 GB), the free space with a bar, and the **Wi-Fi only** switch (on: "Ask before using mobile data").
  - Actions: **Download and continue**, and **Skip voice for now**.
- **The same screen on smaller phones**:
  - **8 GB** (S22, iPhone 16 Pro): Auto picks 1.7B. 4B can still be chosen, with a warning: "Not recommended with 8 GB: slow, and may be closed when memory runs low."
  - **6 GB** (iPhone 15): Auto picks 1.7B. 4B is greyed out with the reason: "Needs 8 GB of memory. This phone has 6 GB."
- **Microphone** (step 3 of 3): what the permission is for, shown before the phone's own prompt. It listens only while you hold the button, with no wake word; speech becomes text on the phone; a voice note keeps its clip for 30 days (shared decision S3). A thin line at the top shows the download already running (4%). Continue, which opens the phone's prompt, or Not now.
- **Downloading**: a page you can leave. It shows the overall percentage and GB, the time left on Wi-Fi, a row per file (Parakeet "downloaded and checked", Qwen3 4B in progress), and four promises: it carries on in the background, resumes after a dropped connection, asks before using mobile data, and checks each file. Actions: **Use ShipLean while this downloads** and Pause download. Use ShipLean leads to **Projects** (`projects.html` `first`), or straight into the project when there is only one (projects.md D5).
- **The app while downloading**: inside a project, its key tile at the top left (projects.md D2) and the banner from work-item `downloading` (the same title, bar and wording: "Voice is getting ready · 2.0 of 3.1 GB on Wi-Fi · about 3 min. Read, tap and type in the meantime.") at the top of list and detail screens. Tapping it opens the Downloading page. The mic is off and labelled Downloading.
- **Mobile data**: when Wi-Fi drops, the banner switches to "Voice download paused · waiting for Wi-Fi" and a sheet asks "Continue on mobile data?", showing how much is left (1.1 GB). The choices are **Wait for Wi-Fi** (primary) or **Use mobile data for this download**.
- **Not enough space**: the download stops and says how much more it needs and how much is free, and keeps what it has. It offers **Try again**, **Open phone storage**, or switching to Qwen3 1.7B, which fits once the partial 4B download is removed. The same page shows a resolved checksum retry on the Parakeet row: "The first copy didn't match its checksum, so it was downloaded again."
- **Voice is ready**: shown once. A coach mark points at the mic: "Try it: hold the button and say 'What needs me?'", with Skip.
- **First command answered**: the answer sheet, in the same look as work-item `ask`, lists the four items waiting on you with their counts (10 decisions, 3 questions). It says nothing was changed and suggests what to try next ("open an item and try 'add a note: …'"). Open Inbox or Done.
- **Settings › Voice** (opened from the Voice row of the Account sheet on Projects, projects.md D3):
  - status ("Voice is ready · Galaxy S24 Ultra · 12 GB memory");
  - **Models**: Command model (Auto · Qwen3 4B, opens the picker), Speech to text (fixed), and a greyed **Try other models**, marked *Later*;
  - **Download and storage**: Wi-Fi only, the storage used by each model, and **Delete models** ("Frees 3.1 GB. Voice turns off; your choices are kept.");
  - **Privacy and licences**: Microphone (opens the phone's settings), and **Model licences** with "Parakeet TDT 0.6B (CC-BY-4.0) · Qwen3 (Apache 2.0)" on the row.
- **Change model**: a sheet with the same three choices. Picking 1.7B on the S24 Ultra says what will happen: it downloads 1.1 GB on Wi-Fi, voice keeps using 4B until the new model is checked, and then 4B is deleted to free 2.5 GB. Cancel, or **Download 1.1 GB**.
- **Voice skipped**: no download. A "Voice is off" banner with **Set up** sits on list screens, and the mic is greyed and labelled **Set up voice**. Both open the model choice screen.

## States

| id | What it shows |
|---|---|
| `welcome` | First open: logo, what the app does, three points, Get started. |
| `sign-in` | Server prefilled and checked ("ShipLean 1.9 · connected"); Sign in opens the browser. |
| `models` | S24 Ultra, 12 GB: Auto → Qwen3 4B, 3.1 GB to download, 87 GB free, Wi-Fi only on. |
| `models-8gb` | S22, 8 GB: Auto → 1.7B, 1.7 GB; 4B can be chosen but carries a warning. |
| `models-6gb` | iPhone 15, 6 GB: Auto → 1.7B; 4B greyed: "Needs 8 GB of memory". |
| `microphone` | Explainer before the phone's prompt; the download is already at 4%. |
| `downloading` | 39%, 1.2 of 3.1 GB; Parakeet done and checked, Qwen3 4B in progress; Use ShipLean while this downloads. |
| `app-downloading` | Backlog with the "Voice is getting ready" banner (65%); the mic off. |
| `mobile-data` | Wi-Fi lost: paused banner, and a sheet asking before 1.1 GB on mobile data. |
| `no-space` | Stopped at 74%: needs 0.8 GB more with 0.3 GB free; a checksum retry on Parakeet that succeeded. |
| `ready` | Coach mark at the mic: "hold the button and say 'What needs me?'". |
| `first-result` | The answer: 4 items, 10 decisions and 3 questions; nothing changed; what to try next. |
| `settings` | Settings › Voice: models, Try other models (Later), Wi-Fi only, storage, Delete models, Microphone, licences. |
| `change-model` | Switching Auto (4B) to 1.7B: 1.1 GB download, 4B kept until the new model is checked. |
| `skipped` | Voice skipped: a "Voice is off · Set up" banner; the mic labelled Set up voice. |

## Voice actions on these screens

None during setup: setup is touch-first, because voice isn't there until the models are downloaded and checked. The only voice on these screens is the guided first command in `ready`. It uses the list screen's read-only `ask` action ("What needs me?"), which changes nothing. Settings › Voice has no voice actions in v1.

## Left out

- The browser confirm page, the sign-in errors and the Projects list: all specified in SHIP-907.
- The **Models screen** (v2, SHIP-906 D4): it shows only as the greyed "Try other models · Later" row.
- Choosing the speech model: Parakeet is fixed in v1.
- Measured speed per phone: the times shown are estimates by phone class until the test set (SHIP-906 design point 7) gives real numbers.
- How download progress shows in a notification and on the lock screen. Each platform has its own system UI for that.
- The full Model licences page: a plain list of each model's name, author, licence, link and changes, built from the pinned model manifest.

## Decisions for Joel

- **D1. Can people skip voice entirely at first run?** Recommended: yes, with "Skip voice for now" on the model screen. Nothing downloads, and voice is set up later from the greyed mic or Settings › Voice. Alternative: no skip, so every install downloads the models at first run, as SHIP-906 D3 reads literally.
- **D2. Should Auto be selected for people, or should they have to pick?** Recommended: Auto selected and marked Recommended, showing what it picked ("This phone: 12 GB → Qwen3 4B"), so one tap continues. Alternative: nothing selected until the person chooses a model.
- **D3. Does sign-in come before the model choice?** Recommended: sign in first (step 1), then models (step 2), so nobody downloads 3 GB for a server they can't reach. Alternative: models first, so the download starts before sign-in.
- **D4. Should the download start before sign-in finishes?** Recommended: no. It starts when the person taps Download and continue, and the microphone step and the rest of the app run while it downloads. Alternative: start the speech model (0.6 GB, the same on every phone) as soon as the app opens, and add the command model after the choice.
- **D5. Can a phone with 8 GB pick Qwen3 4B?** Recommended: yes, with a warning on the option (slow, may be closed when memory runs low). The app still checks free memory before loading, as on iOS. Alternative: 4B only at 11 GB reported or more, greyed out below that.
- **D6. What happens on phones under 8 GB, such as the iPhone 15 (6 GB)?** Recommended: voice is offered with Qwen3 1.7B only, and 4B is shown greyed with the reason; below 6 GB, setup skips voice and says why. Alternative: no voice on any phone under 8 GB, and setup skips the voice step with a line saying why.
- **D7. Should Wi-Fi only be on by default?** Recommended: on, asking each time the download would use mobile data, with the size left; "Use mobile data for this download" applies to that download only. Alternative: off by default, so the download uses whatever connection is there.
- **D8. Does deleting the models keep the voice settings?** Recommended: yes. The model choice and Wi-Fi only are kept, voice turns off, and the mic offers Set up voice, which downloads the same models again. Alternative: deleting resets everything, as if voice had been skipped.
- **D9. Where does the model attribution live?** Recommended: a Model licences row in Settings › Voice that names both licences, opening a page with the full attribution, and the same entries in the app's About › Licences. Alternative: only in the app's general About › Licences list.
- **D10. When does the app ask for the microphone?** Recommended: during setup, right after the model choice, with an explainer first. Alternative: the first time the person holds the mic.
- **D11. When switching models, which goes first: the old one deleted or the new one downloaded?** Recommended: download the new model and check it first, keep using the old one until then, and delete the old one after; the sheet says so. Alternative: delete the old model first to save space, leaving voice off until the new one is ready.
- **D12. What memory figure does the app show?** Recommended: the figure as sold, rounded up from what the phone reports (11.2 GB shows as 12 GB), with Auto's rule still at 11 GB reported. Alternative: show the reported figure ("11.2 GB").
- **D13. Should there be a guided first command once voice is ready?** Recommended: yes, once, a coach mark at the mic with "What needs me?", which is read-only and safe. Alternative: only a toast saying "Voice is ready".
- **D14. Should the model screen show a time per command?** Recommended: yes, an estimate by phone class ("About 5 s a command here"), replaced by numbers from the test set before release. Alternative: relative words only (Quicker, More accurate) with no numbers.

## Open questions

- On Android, the models could come through Play for On-device AI packs, which can arrive with the install itself. If the speech model ships with the install, does the first-run download shrink to the command model only?
- iOS background downloads (Background Assets or a background `URLSession`) may pause when the app is closed by the person. Does the Downloading page need a line about that on iPhone?
- If the 4B model passes the size check but fails the memory check when it loads, should voice fall back to 1.7B? That means downloading another 1.1 GB, or keeping 1.7B on the phone as a fallback.
- If the microphone is refused, what does the mic do: open an explainer with "Open phone settings", or stay greyed with a label?
- Do self-hosted servers download the models from our CDN, or does the ShipLean server host them, for instances with no internet access?
- Should the download pause on low battery or in battery saver?
- Should Delete models also be offered when the phone is low on space, for example from the system's storage warnings?
- Is "Same on every phone" right for the speech model if a smaller one (Moonshine) becomes the fallback for the S22?
