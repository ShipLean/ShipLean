# Projects and switching project (phone app)

Feature: **SHIP-925** (under epic SHIP-905).

Mock-ups: `projects.html` (`?state=<id>`). This area connects three things that were designed separately: SHIP-907 D7 (Projects is the first signed-in screen), SHIP-916's setup (which ends in the tab bar) and the tab bar (which had no project control).

## Purpose

Every tab in the app shows one project. This area answers four questions: where you choose that project, how you can always tell which one you're in, how you switch, and how the app picks a project when it opens. The approach: **Projects is the root of the app**. A project's tabs open on top of it, and the project's key, shown as a tile at the top left of every tab, takes you back to it.

## The gap this closes

Before this, the areas disagreed:

- `backlog.html` had a round "S" avatar labelled *Projects* at the top left.
- In `setup.html`, the Backlog shown during the download had a "JD" avatar labelled *Account* in the same place.
- `feature-board.html`, `retro.html` and `inbox.html` had no control there. The project showed only as "SHIP ·" in the subtitle.
- Setup's "Use ShipLean while this downloads" went straight to Backlog, which skipped Projects (SHIP-907 D7) entirely.

## What's on screen

- **Projects** (the root): the title *Projects* with the server under it. Search and the Account avatar are at the top right, as in SHIP-907. Rows show the project's key tile, its name, a sprint line ("Sprint 25 · 4 days left" or "No sprint running"), and how many items wait on you there (the Inbox count, with a dot when there's something new since you last looked). The list is split into **Recent** (the last 3 you opened) and **All your projects** (alphabetical), as in the web's picker (SHIP-713). The project you're in is highlighted and labelled "You're here · Backlog". At the bottom, a dock holds the mic: "Say a project, or 'what needs me?'".
- **First time** (after setup): there's nothing recent yet, so you see one alphabetical list. The voice download banner from setup sits at the top, and the dock reads "Voice is getting ready · Tap a project to open it".
- **The project tile on every tab root:** the key (2 to 4 capitals, so it always fits) in a filled accent tile at the top left of Inbox, Backlog, Features and Retro. The project name is the subtitle under the screen title. Tap the tile, or swipe back, to reach Projects. A dot on the tile means something new waits on you in another project. Detail screens (a work item, a Feature's board) keep their back button. The tile appears only on tab roots.
- **First time in a project:** the project opens on **Inbox**. A one-time tip points at the tile: "Every tab shows this project. Tap SHIP to switch project or open your account. Or hold the mic and say 'switch to Payments'."
- **Switch by voice:** from any tab, "switch to Payments", "open DATA" or "back to ShipLean". Project names and keys are hotwords. Switching changes no data, so there's no preview: the app goes straight to the same tab in the other project, and a toast says "Now in Payments (PAY)" with **Back to SHIP**.
- **Account** (a sheet from the avatar on Projects): name and email, a **Settings** row that opens the Settings screen (Voice, Notifications, Shortcuts, Appearance, About; `settings.md`), server, server version, **Profile, password and devices** (opens the web), and **Sign out**. The footer gives the app version (SHIP-907 §5).
- **Removed from a project:** when the app reopens a project you've lost access to, you land on Projects with "You're no longer in Partner Portal. Someone removed you, or the project was archived. Ask a project admin to add you again."
- **No projects:** "You're not in any projects yet" (SHIP-907's wording), a line on how to get added, a pointer to create a project on the web, **Check again**, **Open prod.shiplean.app**, and the email you're signed in as.

## How it fits together

1. **First run:** Welcome, then Sign in, Voice models, Microphone and Downloading, then *Use ShipLean while this downloads*. That leads to **Projects**, or straight into the project when there is only one (D5), or to the empty state when there are none.
2. **Into a project:** Inbox the first time (D6), then the tabs. The tile or a swipe back returns to Projects.
3. **Every later launch:** the app reopens the last project on the tab you left (D4). If you've lost access to it, you land on Projects with the notice. A lost session goes to Sign in (SHIP-907).
4. **Switching:** tap the tile and pick a row on Projects, or say "switch to …" from any tab.

## States

| id | What it shows |
|---|---|
| `first` | Projects the first time, after setup: one alphabetical list, needs-you counts, the download banner, the mic off. |
| `first-entry` | First time in a project: Inbox with a one-time tip pointing at the SHIP tile. |
| `tab-root` | Backlog with the SHIP tile, the project name as subtitle and a dot (something new in another project). |
| `projects` | Back on Projects from the tabs: Recent and All, with "You're here · Backlog" on SHIP. |
| `voice` | Listening over Backlog: "Switch to **Payments**". |
| `switched` | Payments' Backlog, with the toast "Now in Payments (PAY) · Back to SHIP". |
| `account` | The Account sheet over Projects: Settings, server, version, the profile on the web, Sign out (`settings.md`). |
| `removed` | Projects with the "You're no longer in Partner Portal" notice. |
| `empty` | No projects: how to get added, Check again, open the web. |
| `alt-sheet` | D1 alternative: the web's switch sheet over the tabs. |
| `alt-title` | D2 alternative: no tile; the project name with a chevron under the title, and the Account avatar at the left. |

## Voice actions on these screens

- On **Projects**: `open` ("open Payments", "open PAY") and `ask` ("what needs me?", answered across all your projects, read only).
- On **any tab root**, added to that screen's own actions: `switch` ("switch to Payments", "back to ShipLean"). It is navigation, so there's no preview and no undo, only the toast's Back.

## Changes made in the other areas

- The kit (`app.css`, `app.js`) has `.pkey`, `.proj-btn`, `App.projTile(key, { dot, ring })` and `App.tabRoot({ title, sub, project, dot, ring, right })`. Its subtitle is the project name, followed by `sub` when that is given.
- `backlog.html`: the "S" avatar is now the SHIP tile, with "ShipLean" as the subtitle.
- `feature-board.html` and `retro.html` have the tile, and "SHIP ·" became "ShipLean ·" (Features' subtitle is shortened to "8 open Features" to fit). The Inbox (`inbox.html`, in progress elsewhere) uses `App.tabRoot` too.
- `setup.html`: the Backlog seen during and after the download (`app-downloading`, `mobile-data`, `ready`, `first-result`, `skipped`) has the tile instead of the JD avatar. In `setup.md`, *Use ShipLean while this downloads* leads to Projects, and Settings › Voice opens from Account › Settings (`settings.md`).

## Decisions

Joel answered all eleven on 9 Nov 2026 in the Lavish review, each with the recommended option. The alternatives stay as the `alt-sheet` and `alt-title` states, marked as turned down.


- **D1. Where does switching project live?** **Decided (recommended):** Projects is the root of the app, the project's tabs open on top of it, and the tile (or a swipe back) returns to it. That makes SHIP-907's Projects screen and Account the one place for both. Alternative: the web's switch sheet (SHIP-713) slides up over the tabs, and the Projects screen only appears at first run or with no project.
- **D2. What on a tab root shows and switches the project?** **Decided (recommended):** the project's key tile at the top left of every tab root, replacing Backlog's "S" avatar and setup's "JD" avatar, with the project name as the subtitle. Alternative: the project name with a ⌄ under the screen title, and the Account avatar at the top left.
- **D3. Where is Account?** **Decided (recommended):** only on Projects (the avatar at the top right, as in SHIP-907). It holds Sign out and the way into Settings (`settings.md` D1–D3). Alternative: on every tab root as well.
- **D4. What does the app open on after the first run?** **Decided (recommended):** the last project, on the tab you left, with Projects one tap away. Alternative: always Projects.
- **D5. What happens with exactly one project?** **Decided (recommended):** skip Projects after sign-in and setup and go straight in. The tile still opens Projects. Alternative: always show Projects first.
- **D6. Which tab opens when you switch?** **Decided (recommended):** the tab you're on (Backlog leads to Payments' Backlog). A project you've never opened starts on Inbox. Alternative: each project remembers its own last tab.
- **D7. Does the app show what waits on you in other projects?** **Decided (recommended):** yes. Each row on Projects shows its needs-you count, and the tile gets a dot when something new waits on you in another project. Alternative: keys and names only (SHIP-907's minimum), with no signal across projects.
- **D8. Can you switch by voice?** **Decided (recommended):** yes. Projects has a mic for "open Payments" and "what needs me?", and "switch to <project>" works from any tab. Switching changes no data, so it has no preview, just a toast with Back. Alternative: touch only for switching. The mic stays for the screen's own actions.
- **D9. How is the Projects list ordered?** **Decided (recommended):** Recent (the last 3 opened), then All alphabetical, as in the web picker. The first time, a single alphabetical list. Alternative: one list sorted by how much waits on you.
- **D10. Can you create a project in the app?** **Decided (recommended):** no. The empty state says how to get added, and points to the web for creating one. Alternative: a New project form in the app.
- **D11. What happens when the project the app reopens is gone?** **Decided (recommended):** land on Projects with a notice saying which project and why. Alternative: quietly open the next most recent project.

## Open questions

- Is Inbox per project, as the mock-ups show, or should there also be an "All projects" Inbox? Projects' needs-you counts cover the cross-project view for now.
- When you're in SHIP and say a key from another project ("approve PAY-31"), should the preview act there and label the change with its project, or offer to switch first?
- When push notifications and links arrive (out of scope in SHIP-907), should opening an item in another project switch project silently, with the same toast?
- Should Projects show anything about each project beyond the sprint line, such as health or the number of Features at risk?
