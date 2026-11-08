# Web screens for the phone app (SHIP-907)

The native app decisions (epic SHIP-905, SHIP-907 foundations, SHIP-916 shared decision S5) add three things to the web app:

1. **The confirm page `/mobile/sign-in`**: the app's browser handoff (SHIP-907 §4, D1).
2. **Your profile › Signed-in devices**: listing and signing out device sessions (SHIP-907 §3).
3. **Health on Features**: On track, At risk or Blocked, set by people and agents and shown on the web too (S5).

Joel answered all 21 decisions in a Lavish review on 9 Nov 2026. He kept every recommendation except **H3**, and added a note on **H1**. SHIP-907 asks for CT baselines of the first two screens; see [CT baselines](#ct-baselines).

## Files

| Mock | States |
|---|---|
| `mobile/device-sign-in.html` | `?state=` (none) confirm · working · error · done · cancelled · missing · proxy · long; `?tab=ios\|android` draws the system browser around the page (a review aid); `?alt=shell\|side` are the alternatives not taken (C1, C3) |
| `mobile/sign-in.html?for=device` | `/login` when the returnUrl is `/mobile/sign-in` (C4) |
| `screens/profile.html` | The Signed-in devices panel. `?devout=<id>` opens the sign-out confirmation, `?devices=0` shows none, and `?shot&at=sessions` (desktop) or `?at=devices` (phone) frames it |
| `screens/feature.html` | Health in the status band and in Edit status. `?health=ok\|risk\|blocked\|none`, `?menu=health` |
| `screens/features.html` | The Health column at the Features level |
| `screens/features-board.html` | Health on cards. `?compact`, `?fhealth=ok\|risk\|blocked\|none` (Filter), `?sheet=filter` (phone) |

Images: `mobile/images/device-sign-in*.png`, `sign-in-for-device.*`, `profile-devices*.*`, `feature-health*.*`, `fboard-health-filter.*` (390 × 844), and `screens/feature*.png`, `features*.png`, `profile-devices*.png` (1440 × 900), each in light and dark. Shared pieces: the `.health` class in `screens/shiplean.css`, `SL.HEALTH` and `SL.health(h, dotOnly)` in `screens/shared.js`.

## 1. The confirm page, `/mobile/sign-in`

The app opens this page in the phone's own browser sheet: an iOS auth session or an Android Custom Tab. So in practice it is only seen at phone width. It belongs to the SHIP-736 phone sign-in family: the bare 48px bar, a card at the top, a full-width primary button. Its content follows the agent consent page (`/authorize`): who is asking, as whom, a warning, and two buttons.

Flow: App Sign in → system browser sheet → `/login?returnUrl=…` (only if signed out) → `/mobile/sign-in` → `shiplean://signed-in?code=…&state=…` → the app exchanges the code and opens Projects.

**Content:** the heading names the device, worded as the spec has it: "Sign in to ShipLean on ‹device›?", with the subtitle "The ShipLean app wants to sign in as you". Under it come three rows:
- **As:** the avatar, name and email.
- **On:** the device name, labelled "The name the app sent".
- **To:** the instance name and host.

Then **What this does**, in three lines:
- "The ShipLean app on that phone can see and change everything you can here."
- "It stays signed in until you sign it out, or until it goes 90 days unused."
- "Sign it out any time from Your profile › Signed-in devices."

Then the consent page's warn callout: "Only sign in if you just tapped **Sign in** in the ShipLean app. If you didn't, tap Cancel." Last come **Sign in** and **Cancel**.

**Other states:**
- **Signing in…:** both buttons are off while `POST codes` runs. Then the page navigates to the app and the sheet closes.
- **Couldn't reach ShipLean:** "Couldn't reach ShipLean. Check your connection and try again." Both buttons stay on.
- **Missing or bad parameters:** "This page opens from the ShipLean app. Start signing in there to get here." There are no buttons.
- **Identity-proxy server:** the page says the app can't sign in to this server yet, because it signs people in through their company's sign-in page, and suggests using the phone's browser instead. **Back to the app** returns `shiplean://signed-in?error=unsupported&state=…`.
- **Done / cancelled:** only seen when the page was opened in an ordinary tab. Done reads "Signed in on ‹device›", with "Open the app". Cancelled reads "Nothing was signed in".
- **Long device name:** wraps in the heading and in the On row. Nothing is cut.

## 2. Your profile › Signed-in devices

This is a panel between **Sessions and saved views** and **Connected agents**. Its rows follow the Connected agents rows:
- a phone icon;
- the name the app sent;
- the session prefix (`shl_dev_k3Xq9TbA…`);
- "Last used ‹when› · Signed in ‹day›";
- a ghost **Sign out** button.

**Sign out** opens the warn confirmation row: "Sign out **‹device›**? The app there goes back to Sign in straight away. Its server and email stay filled in." The row has Cancel and Sign out, and confirming shows the toast "‹device› is signed out."

When the list is empty, the panel says: "No phones are signed in." The footer always says: "To add a phone, sign in to **‹host›** from the ShipLean app."

The browser's own session is a cookie, so no row is ever "this device".

**Copy changes elsewhere on the page.** A password change and *Sign out everywhere else* both rotate the security stamp, which ends device sessions, so the copy now says so:
- the Password meta line: "Changing it signs out your other sessions and your phones, and disconnects your connected agents";
- the Sign out everywhere else text: "…signs out your phones and disconnects your connected agents";
- the matching toasts.

At 900px and below the panel uses SHIP-733's `profile-touch` layout unchanged. In the confirmation row, the text goes above Cancel and Sign out, which split the width equally.

## 3. Health on Features (S5)

Health is **only on Features**. Epics, stories, bugs and tasks never have it (Joel, H1 note). It is set by a person or an agent and never computed.

It looks the same as in the app: an 8px dot and a 600-weight label, coloured `--color-ok` for On track, `--color-warn` for At risk and `--color-danger` for Blocked. When it isn't set, there's a hollow dot.

- **Feature form, status band:** a pill button right after the state pill, showing the dot, the label and ▾. It opens a menu: On track, At risk and Blocked, each with a one-line hint, then Clear health. With no health set, the button reads "Set health". The change waits for Save and writes a History line ("Health ● On track → ● At risk").
- **Edit status:** a segmented control (On track | At risk | Blocked | Not set) above the status line. People and agents usually update both at once.
- **Verified at (H3):** any Health change is a status write and moves Verified at, the same as editing the status line. Still true confirms the line and the health together. There is no separate "Health set by" line.
- **Features backlog:** a Health column after State, on by default, at the Features level only. It shows "—" when not set.
- **Features board:**
  - Full cards show the dot and label at the right of the parent line, with the Epic name shortened so the card doesn't grow.
  - Compact cards show only the dot after the key, with "Health: At risk" as the tooltip and aria-label.
  - Cards with no health show nothing, and cards in Done never show health.
  - There's no card tint by default.
- **Filter:** the Features board filter has Any health, On track, At risk, Blocked and Not set, in the desktop Filter and the phone filter sheet.
- **Phone (900px and below):** in the status band, Health sits on the state pill's row and is 36px tall. The menu opens as the item page's field sheet, titled Health.
- **Agents:** `set_status` gets an optional `health` parameter (H9).
- **The Blocked state (H8):** the reference form's Blocked state was designed but never built, and it goes. Health Blocked stays, and the status line says what is blocking it. Parked is a separate question. Real Feature states stay New, Active, Resolved, Closed and Removed.

## Decisions (Joel, 9 Nov 2026)

| # | Question | Answer |
|---|---|---|
| C1 | Does the confirm page sit in the app shell, like /authorize? | **No: on its own**, with the 48px sign-in bar and no Menu or Search. The drawer would only lead away from the handoff. |
| C2 | How much does the page say? | **As / On / To, three lines on what it does, and the warning.** Another app can start this flow. |
| C3 | Where do Sign in and Cancel go? | **Full width in the footer: Sign in, then Cancel under it** (SHIP-736 D4) |
| C4 | Does /login say why when it comes from the app? | **Yes:** one info line naming the device, and "for the ShipLean app" under the title |
| C5 | Identity-proxy servers (SHIP-907 D8) | **The page says the app can't sign in here.** Back to the app returns `error=unsupported`, and the app shows "This server doesn't support the app yet." |
| C6 | Done and cancelled states after the redirect? | **Yes**, for when the page was opened in an ordinary tab |
| V1 | Where does the panel go? | **Its own panel, after Sessions and saved views and before Connected agents** |
| V2 | What does a row show? | **Name, session prefix, Last used · Signed in.** The prefix tells two phones with the same name apart. |
| V3 | Does Sign out ask first? | **Yes, in the row, like Revoke and Leave** (SHIP-733 D4) |
| V4 | Do the Password and Sign out everywhere else lines mention phones? | **Yes** |
| V5 | Does the panel say how to add a phone? | **Yes, a footer line.** No store links until there's a listing (SHIP-907 D6). |
| V6 | Analytics in SHIP-907? | **Yes:** `device_session.created`, and `device_session.ended` with `reason` = app_sign_out \| web_sign_out \| expired \| stamp_changed \| user_disabled |
| H1 | Where does Health sit on the Feature form? | **In the status band, a pill button after the state.** *Note: Health is only for Features, not Epics, stories or tasks.* |
| H2 | What does "no health" look like? | **"Set health" with a hollow dot on the form, "—" in the backlog, nothing on cards** |
| H3 | Does a Health change on its own move Verified at? | **Yes (changed from the recommendation): any Health change is a status write and sets Verified at** |
| H4 | Is Health in Edit status too? | **Yes, a segmented control above the status line** |
| H5 | Do Epics get Health? | **No.** It would have to be computed, and Health is never computed. |
| H6 | How do board cards show it? | **Full: dot and label on the parent line. Compact: the dot. None in Done.** |
| H7 | Health filter on the Features board? | **Yes**, desktop and phone |
| H8 | Health Blocked or the unbuilt Blocked state? | **Health Blocked stays; drop the unbuilt Blocked state.** The status line says what blocks it. |
| H9 | How do agents set Health? | **An optional `health` parameter on `set_status`** |

## CT baselines

Each in light and dark:

- **`MobileSignInPage.spec.tsx`:** confirm (390 × 844 and 1440 × 900), long device name, couldn't reach ShipLean, missing parameters, identity-proxy server.
- **`SignedInDevices.spec.tsx`:** two devices (1440 × 900); the Sign out confirmation (1440 × 900 and 390 × 844); no phones; two devices at 390 × 844.
- **`LoginPage.spec.tsx`:** for the app (`returnUrl=/mobile/sign-in…`), 390 × 844.

## Measuring

Health changes go out as a `workItem.updated` property change (`field: health`, with `by` set to person or agent), not a new event.

| Compare | Over |
|---|---|
| `/mobile/sign-in` views per week against `device_session.created` per week: how many handoffs finish | The first 8 weeks after the app ships |
| App sign-ins against web sign-ins per week | The same 8 weeks |
| Devices signed out from the web against signed out from the app, per month | Monthly |
| Features with health set, as a share of Active Features, per week, split by person or agent | Weekly |

## Not covered yet

- The phone Features backlog (`mobile/backlogs.html`) doesn't show Health yet.
- Card style rules can't match on Health. `board-settings.html` covers only the Stories board.
- The Health colours are the same as some state-dot colours (On track and Closed, At risk and Resolved, Blocked and Removed). Where the two dots sit side by side, only the label tells them apart.
- The At risk amber (`--color-warn`) as small text in the light theme is probably under 4.5:1 contrast. It's the app's colour, so it was kept.
