# Store listing text (SHIP-930)

Draft for the first release, in English. The same text goes to both stores where the fields match. Limits are the stores' own. Count Apple's keywords in bytes. All the others are character counts.

The description lists only what the app does in the mock-ups (SHIP-916 and the areas after it). Before submitting, trim anything the first release doesn't ship.

## Both stores

| Field | Limit | Text |
|---|---|---|
| App name | 30 | ShipLean |

## App Store (iOS)

| Field | Limit | Text |
|---|---|---|
| Subtitle | 30 | Your backlog, by voice |
| Promotional text | 170 | Answer the questions and decisions waiting on you, see how every Feature is going, and move work by voice. Every change shows on screen before it applies. |
| Keywords | 100 bytes | agile,scrum,sprint,kanban,planning,roadmap,retro,retrospective,features,team,project,agents,approval |
| Primary category | | Productivity |
| Secondary category | | Business |
| What's New | 4000 | (not shown for the first version) |

Keywords leave out "ShipLean", "backlog" and "voice", because Apple already indexes the name and subtitle.

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
| 1 | inbox · list | Everything waiting on you | Questions, decisions and approvals from people and agents, in one list. |
| 2 | backlog · listening | Hold to talk | Say what to change. Keys and names on screen are matched as you speak. |
| 3 | work-item · preview | Read it before it changes | Every voice command shows its changes first. Nothing applies until you say yes. |
| 4 | feature-board · board | See how every Feature is going | On track, at risk or blocked, and what needs you. |
| 5 | work-item · ask | Ask, then read the answer | "What's left on this?" answered on screen, from the item as it is now. |
| 6 | retro · retro | Run the retro from your phone | Add notes by voice and decide which become action items. |
| 7 | search · results | Find any work item | The same search as ShipLean on the web, typed or spoken. |
| 8 | setup · models | Speech recognised on your phone | Voice models download once and run on the phone. |

The captions follow the app's tenet, speak in and read out. None of them promises read aloud or spoken answers. Google Play turns down words like "best", "#1", "new" and "free", and calls to action like "download now", so the captions avoid them.
