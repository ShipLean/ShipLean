# Account and Settings (phone app)

Mock-ups: `settings.html` (`?state=<id>`). Decisions D1–D9 answered 9 Nov 2026 (all recommended). Feature: **SHIP-929** (under epic SHIP-905), next to SHIP-907 (which builds the Account sheet) and SHIP-925 (Projects).

## Purpose

Settings › Voice is drawn (`setup.html` `settings`), and so is Settings › Shortcuts (`search.html` `settings`), but not the screen that leads to them. SHIP-907's Account sheet had a **Voice** row that went straight to Settings › Voice, so there was nowhere for Notifications (Inbox D13), Shortcuts (Search D11), Appearance or the app's licences (setup D9). This area draws the **Settings** screen and the way to it from Account.

The rule that splits the two: **Account is you on the server; Settings is this phone.** Account shows who you are, which server you're on, the way to your profile on the web, and Sign out. Settings holds what's kept on this phone and stays there when you sign out.

## What's on screen

- **Account** (the sheet from the avatar on Projects, SHIP-907 §5, projects.md D3):
  - your avatar, name and email;
  - a **Settings** row at the top ("Voice, notifications, shortcuts, appearance"), which replaces the old Voice row;
  - Server and Server version (SHIP-907);
  - **Profile, password and devices**, marked *On the web*, which opens your Profile on the server in the browser;
  - **Sign out**, and the app version at the foot.
- **Settings** (a full screen; Back returns to Projects). Its subtitle is "This phone · Galaxy S24 Ultra".
  - **On this phone:** Voice ("Ready · Qwen3 4B", opens Settings › Voice), Notifications ("On"), Shortcuts ("2 on", opens Settings › Shortcuts from `search.html`) and Appearance ("As the phone").
  - **About:** Help (the docs, in the browser), Licences, Privacy policy (in the browser) and Version ("1.0.0 (12) · Android 15").
  - A footer: these settings are kept on this phone and stay when you sign out; your name, password and signed-in devices are on the web, under Account › Profile.
- **Settings › Notifications:**
  - an example of what arrives, drawn as two lock-screen notifications (an agent's ask on SHIP-907, Priya's mention on PAY-41); tapping one opens the item in its project;
  - **Notify me when:** *Someone asks me* (on), *I'm mentioned* (on), and *An item I own gets an ask* (off: counted on the badge either way). These follow Inbox D13;
  - **App icon:** *Badge*, the asks waiting on you (15), as on the Inbox tab (Inbox D11);
  - a footer: the choices cover all your projects, and sound, the lock screen and Focus are set in the phone's settings.
- **Notifications blocked:** when the phone blocks ShipLean, a notice says so ("nothing below is sent and the app icon shows no badge"), the switches grey out, and **Open phone settings** is the one action.
- **Settings › Appearance:** *As the phone* (the default), *Light* or *Dark*, drawn as three small phones. Kept on this phone, as the web keeps it per browser (SHIP-745).
- **Settings › Licences:** the voice models first (Parakeet TDT 0.6B, NVIDIA, CC-BY-4.0; Qwen3 4B, Alibaba Cloud, Apache 2.0), then the app's open source packages and its own licence. It's setup D9's About › Licences. Settings › Voice keeps its Model licences row.
- **When something needs you:** voice skipped or deleted, or notifications blocked. A dot sits on the avatar on Projects; the Account sheet's Settings row reads "Voice is off on this phone" with **Set up**; Settings shows "2 things need you on this phone" at the top, and the rows say what is wrong.
- **Sign out:** a confirm sheet, "Sign out of prod.shiplean.app?". It says the voice models (3.1 GB) and these settings stay, the server and email stay filled in (SHIP-907), and recent searches and what was loaded for reading are removed.

## How it fits together

1. Projects › avatar › **Account** sheet › **Settings** › Voice, Notifications, Shortcuts, Appearance or Licences.
2. The app's own prompts still go straight to the page they need, without passing through Account: the download banner opens the Downloading page; *Voice is off · Set up* and the greyed mic open setup's model choice; Inbox's push opt-in (`empty-new`) asks the phone, and if the phone says no, it opens Settings › Notifications.
3. Account stays on Projects only (projects.md D3), so Settings is two taps from Projects, and from a tab it's the key tile first.

## States

| id | What it shows |
|---|---|
| `account` | The Account sheet over Projects: Settings row first, server, version, profile on the web, Sign out. |
| `settings` | Settings: Voice, Notifications, Shortcuts, Appearance; Help, Licences, Privacy policy, Version. |
| `notifications` | Settings › Notifications: the example notifications, three switches, the badge. |
| `notifications-off` | The phone blocks notifications: the notice, Open phone settings, the switches greyed. |
| `appearance` | Settings › Appearance: As the phone, Light, Dark. |
| `licences` | Settings › Licences: the two voice models, then the app's packages. |
| `account-attention` | Voice is off: a dot on the avatar, and the Settings row with Set up. |
| `settings-attention` | Settings with voice off and notifications blocked: the notice and the rows. |
| `sign-out` | The Sign out confirm sheet: what stays and what goes. |
| `alt-one-screen` | D1 alternative: Account as one full screen with the settings rows inline. |
| `alt-gear` | D1 alternative: a gear next to the avatar on Projects. |

Settings › Voice is `setup.html` `settings` and `change-model`; Settings › Shortcuts is `search.html` `settings`. Neither changes.

## Voice actions on these screens

None. Settings is touch only, as Settings › Voice already is (setup.md). Navigation by voice is an open question.

## Changes made in other areas

- `projects.html` `account`: the Voice and "This phone" rows are replaced by the Settings row and the web profile row, as drawn here. `projects.md`: D3 and the Account bullet now say "the way into Settings".
- `setup.md`: Settings › Voice opens from Account › Settings.
- SHIP-907 §5's Account sheet gains the Settings row and the web profile row. Settings itself isn't in SHIP-907's scope.

## Decisions

Joel answered all nine on 9 Nov 2026 in the Lavish review, each with the recommended option. The D1 alternatives stay as the `alt-one-screen` and `alt-gear` states, marked as turned down.


- **D1. How does Account lead to Settings?** **Decided (recommended):** one **Settings** row at the top of the Account sheet. Settings is a full screen, and Back returns to Projects. Alternative: Account becomes one full screen with the settings rows inline (`alt-one-screen`), or Settings gets its own gear next to the avatar on Projects (`alt-gear`).
- **D2. What goes on Account, and what in Settings?** **Decided (recommended):** Account is you on the server: name, email, server, version, your profile on the web, and Sign out. Settings is this phone: Voice, Notifications, Shortcuts, Appearance and About. Alternative: one list with everything.
- **D3. Does Account keep its direct Voice row?** **Decided (recommended):** no. Voice moves into Settings. The Settings row on Account shows voice's state when it needs you, and the banner and the greyed mic still go straight to voice setup. Alternative: keep a Voice row on Account as a shortcut, above Settings.
- **D4. What's in Settings in v1?** **Decided (recommended):** Voice, Notifications, Shortcuts and Appearance, then About (Help, Licences, Privacy policy, Version). Alternative: Voice, Shortcuts and About only; notifications and appearance follow the phone's own settings.
- **D5. What can be chosen for notifications?** **Decided (recommended):** three switches (asks to you by name on, mentions on, asks on items you own off), plus the badge, for all your projects at once. Alternative: the same switches per project as well.
- **D6. Is there an Appearance choice?** **Decided (recommended):** yes. As the phone (the default), Light or Dark, kept on this phone like the web's choice. Alternative: always follow the phone, as SHIP-907 has it, with no page.
- **D7. Where are name, password and signed-in devices changed?** **Decided (recommended):** on the web. Account has one *Profile, password and devices* row that opens your Profile in the browser. Alternative: edit your name and list signed-in devices in the app.
- **D8. What does Sign out do?** **Decided (recommended):** a confirm sheet. The voice models and settings stay on the phone, the server and email stay filled in, and recent searches and loaded data are removed. Alternative: no confirm sheet, or also delete the voice models.
- **D9. How does the app show that a setting needs you?** **Decided (recommended):** a dot on the avatar on Projects, the reason on Account's Settings row, and a notice at the top of Settings. Alternative: no dot; it shows only inside Settings and in the app's own banners.

## Open questions

- Should "open settings" or "voice settings" work by voice from Projects, as "open Payments" does? It changes no data, like switching project.
- Self-hosted servers: whose privacy policy does the row open, ShipLean's or the server operator's? Could `/api/health` return a link?
- Notification choices have to reach the server, because the server sends the pushes. Do they live on the device session (SHIP-907 §3), so each phone has its own?
- Should About have a *Report a problem* row that attaches the app's logs, or is that left to Help?
