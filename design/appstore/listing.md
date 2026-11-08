# Store listing text (SHIP-930)

Draft for the first release, in English. The same text goes to both stores where the fields match. Limits are the stores' own. Count Apple's keywords in bytes. All the others are character counts.

The description lists only what the app does in the mock-ups (SHIP-916 and the areas after it). Before submitting, trim anything the first release doesn't ship.

## Both stores

| Field | Limit | Text |
|---|---|---|
| App name | 30 | ShipLean: Backlog by Voice |

## App Store (iOS)

| Field | Limit | Text |
|---|---|---|
| Subtitle | 30 | Plan with your team and agents |
| Promotional text | 170 | Answer the questions and decisions waiting on you, see how every Feature is going, and move work by voice. Every change shows on screen before it applies. |
| Keywords | 100 bytes | agile,scrum,sprint,kanban,planning,roadmap,retro,retrospective,features,project,approval,inbox,notes |
| Primary category | | Productivity |
| Secondary category | | Developer Tools |
| What's New | 4000 | (not shown for the first version) |

Keywords leave out the words already in the name and subtitle (ShipLean, backlog, voice, team, agents), because Apple indexes those already.

## Google Play

| Field | Limit | Text |
|---|---|---|
| Short description | 80 | Answer what's waiting on you, see progress and move work by voice. |
| Category | | Productivity |

## Description (both stores, 4000)

```
Keep up with your team's work, answer what's waiting on you, and change priorities by voice.

ShipLean is a lean planning tool for small teams and the agents that work with them. The phone app is for the times you're away from your desk: checking progress, answering questions, approving decisions and moving work up or down the backlog.

HOLD TO TALK
• Hold the mic and say what you want: "move SHIP-12 above SHIP-9", "add a note: customers want this before Friday", "what's left on this Feature?"
• Keys, names and states on the screen are matched as you speak.
• Every command shows its changes before anything happens. Nothing changes until you tap Apply or say "yes", and you can undo it.
• Speech is recognised on the phone, with voice models you download once.

EVERYTHING WAITING ON YOU
• The Inbox lists the questions, decisions and approvals waiting on you, from people and agents, longest waiting first.
• Answer from the list: tap a quick answer, approve, or hold to reply.
• Your answer goes on the work item, for the next person or agent who picks it up.

SEE HOW THINGS ARE GOING
• Features shows progress across every Feature: on track, at risk or blocked, and what needs you.
• "Since yesterday" sums up what changed.
• Backlogs for Epics, Features and Stories, in rank order.

RETROS
• Add retro notes by voice during the sprint, and decide which ones become action items.

FIND ANY WORK ITEM
• The same search as ShipLean on the web, typed or spoken.

WORKS WITH YOUR SERVER
• Sign in to prod.shiplean.app or your own ShipLean server. The app uses your server's own sign-in page, in the browser.
• Switch between your projects, choose light or dark, and get a notification when someone asks you something by name.

You need a ShipLean account to use the app.
```

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
