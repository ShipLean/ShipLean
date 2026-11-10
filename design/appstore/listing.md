# Store listing (SHIP-930)

The listing text lives in [`store/`](../../store/README.md), in fastlane's layout, and is pushed to both stores from CI (SHIP-1060). Change the text there, in a pull request. This file keeps the screenshot captions, the stores' limits and Joel's decisions.

## Limits

The stores' own limits, which the Store listing check enforces on `store/`. Count Apple's keywords in bytes. All the others are character counts.

| Field | Store | Limit | File in `store/` |
|---|---|---|---|
| App name | both | 30 | `ios/metadata/en-US/name.txt`, `android/metadata/en-US/title.txt` |
| Subtitle | iOS | 30 | `ios/metadata/en-US/subtitle.txt` |
| Promotional text | iOS | 170 | `ios/metadata/en-US/promotional_text.txt` |
| Keywords | iOS | 100 bytes | `ios/metadata/en-US/keywords.txt` |
| What's New | iOS | 4000 | none for the first version |
| Short description | Play | 80 | `android/metadata/en-US/short_description.txt` |
| Description | both | 4000 | `ios/metadata/en-US/description.txt`, `android/metadata/en-US/full_description.txt` |

The same text goes to both stores where the fields match. The description lists only what the app does in the mock-ups (SHIP-916 and the areas after it). Before submitting, trim anything the first release doesn't ship.

Keywords leave out the words already in the name and subtitle (ShipLean, backlog, voice, team, agents), because Apple indexes those already.

Categories (D7): Productivity, with Developer Tools second on iOS. Play takes one category, Productivity, set by hand in Play Console.

## Screenshot captions

| # | Screen (mock-up state) | Headline | Line under it |
|---|---|---|---|
| 1 | backlog · listening | Hold to talk | Say what to change. Keys and names on screen are matched as you speak. |
| 2 | work-item · preview | Read it before it changes | Every voice command shows its changes first. Nothing applies until you say yes. |
| 3 | inbox · list | Everything waiting on you | Questions, decisions and approvals from people and agents, in one list. |
| 4 | feature-board · board | See how every Feature is going | On track, at risk or blocked, and what needs you. |
| 5 | work-item · ask | Ask, then read the answer | "What's left on this?" answered on screen, from the item as it is now. |
| 6 | retro · retro | Run the retro from your phone (tablets: from anywhere) | Add notes by voice and decide which become action items. |
| 7 | search · results | Find any work item | The same search as ShipLean on the web, typed or spoken. |
| 8 | setup · models | Speech recognised on your phone (tablets: your tablet) | Voice models download once and run on the phone (tablets: the tablet). |

The captions follow the app's tenet, speak in and read out. None of them promises read aloud or spoken answers. Google Play turns down words like "best", "#1", "new" and "free", and calls to action like "download now", so the captions avoid them.

## Decisions (Joel, 9 Nov 2026, Lavish review)

| # | Question | Answer |
|---|---|---|
| D1 | App name | **ShipLean: Backlog by Voice** (alternative). The iOS subtitle changed so it doesn't repeat the name |
| D2 | Screenshot background | Brand gradient, white captions |
| D3 | Theme of the screens | Light |
| D4 | Data on the final shots | A made-up demo team project, retaken from the built app. The same account is the reviewers' sign-in |
| D5 | Tablets | **Support iPad and Android tablets** (alternative). The draft tablet shots draw the phone layout at tablet size. The app needs a tablet layout before the final shots |
| D6 | iOS 26 icon | Flat icons now. The Icon Composer file is made from the mark's SVG when the app is built |
| D7 | Categories | **Productivity, with Developer Tools second on iOS** (alternative). Play takes one category: Productivity |
| D8 | Preview video | None for the first release |
| D9 | Feature graphic | As drawn |
| D10 | Screenshot order | **Hold to talk, then Preview, then Inbox** (alternative) |
