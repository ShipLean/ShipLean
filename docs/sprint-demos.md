# Sprint demos

A sprint demo is a short narrated video of what a sprint finished. An agent makes it from the sprint's own items, on its own machine, and sends it back to ShipLean. The sprint's Demo tab plays it, with its chapters, the features it covers and its script. ShipLean never renders video itself.

**The YouTube check is not proven against Google yet.** It has been run against a fake and a local stub, never against Google. Everything about YouTube below says what the code does, not what Google has been seen to do. The steps for proving it are in [The live spike](#the-live-spike), and [Not proven yet](#not-proven-yet) lists what stays open until someone runs them.

## What a person does

1. **Close the sprint.** Demos are for closed sprints. On a sprint that isn't closed, the Demo tab says "Close the sprint to make its demo." and Create demo is off.
2. **Either wait or ask.**
   - If the project has **Automatic demos** on (Project settings › Demos), closing the sprint starts the agent by itself. This works for Close sprint and for the MCP tool `end_sprint`. A sprint nobody closes gets no automatic demo.
   - Otherwise open the sprint's **Demo** tab (after Retro) and press **Create demo**. This is also how to retry, and how to make a demo for a sprint that was closed with the setting off.
3. **Watch the status.** The tab's band shows where the demo is (below). It refetches by itself when the demo changes, so it needs no reload.
4. **Play it.** When the status is Ready the tab shows the player, the chapters (a click seeks), the features the demo covers and the script. **Remake demo** asks the agent again; the old video stays live until the new one arrives.

Create demo is off, with the reason shown, when the sprint isn't closed, when an agent session is already making this demo, or when the project has no agent connection or no repository (the check `AgentStartBlock` makes for any agent start). Everyone on the project can press it; the settings need a project admin.

### The statuses

| Status | Chip | Means |
|---|---|---|
| `closable` | Not closed | The sprint is still open. |
| `none` | No demo yet | Closed, and nobody has asked for a demo. |
| `requested` | Requested | A demo was asked for and no agent session is live yet. |
| `working` | Working | An agent session is live on the demo's BAU item. The band links to the item. |
| `ready` | Ready | A video arrived after the last request. |
| `failed` | Failed | The upload link expired before a video came back, or the agent session ended as failed, canceled or stale without one. The message says which. |
| `blocked` | Blocked | The demo could not start. The message says why: no agent connection, no repository, no base URL on the instance, or "The demo could not be started. Start it again from the sprint's demo panel." |

The first match wins, in this order: blocked, working, ready, failed, requested. The band also shows when the demo was asked for and, while it works, when the upload link runs out. Create demo reads **Create demo again** after a failure and **Remake demo** when Ready.

## What an agent gets

The prompt, as Markdown, plus a one-time upload link. There are two ways an agent receives it:

- **ShipLean starts the agent** (Create demo, or Automatic demos). It starts a session on the demo's BAU item through the project's agent connection, and the prompt is the session's body. The relay is unchanged.
- **An agent asks** with the MCP tool `create_sprint_demo` ([mcp.md](mcp.md#feature-editor-tools)). It always works: no closed-sprint check and no agent check. The one refusal is a live agent session already making this demo. With no `sprint` it takes the most recently closed sprint, else the current one. It fails, like `attach_file`, when the only host ShipLean knows is a cluster service name; set `base_url`.

What the prompt holds, in order:

- **The task.** One video: an intro, one chapter per Feature in the sprint, a short "Also this sprint" section, then a closing "What we'll measure".
- **The sprint's data, inline.** Per Feature: its goal, its **why** and **measure** (the `## Why` section of the Feature's description, see below), its stories with summaries and acceptance criteria, plan facts (waves, stories, tasks, review rounds, pull requests), its retro points and attachment names. Also the sprint's other work, the sprint's own retro points and its action items. The agent needn't open any item. The data is capped at 120 KB; past that, some Features keep only a header and `truncated` is true, and the prompt tells the agent to say so in the intro.
- **No names, no raw logs.** The data carries retro points by theme and category, never who wrote them, never a root cause or raw failure log, and no comment bodies, so a report comment can't reach the video. The prompt also forbids putting any of that on a slide or in the narration.
- **Presenters.** Three synthetic voices from the free local model Kokoro. Ann (`af_heart`) does the intro, each Feature's why and the measurement. Keith (`bm_george`) does before and after, the walkthrough clip and the implementation. Becky (`bf_emma`) does how the agents built it, the retro items and "Also this sprint". Each introduces themselves once and hands over out loud.
- **Honesty.** The video narrates the why and the stated measure, never results; nothing has been measured. A Feature with no recorded why is said to have none.
- **The environment.** The project's own text on how to start a local seeded copy (Project settings › Demos). The agent records against that copy, **never against production** or the instance the data came from, and says on screen that the data is made up.
- **Branding.** The project's logo and design tokens, or ShipLean's (see [A project's logo and tokens](#a-projects-logo-and-tokens)).
- **The upload command** and what to do with it.

Each Feature needs a `## Why` section in its description for the demo to say anything about why it was done: the text up to the next `## ` heading, with the measure taken from a line starting `**Measure`. A measure marked `(to confirm)` is narrated as to confirm. `planning_guide` asks for the section.

### The upload

The prompt ends with one `curl`:

```bash
curl --fail-with-body -F chapters=@chapters.json -F script=@script.md -F video=@demo.mp4 '<upload url>'
```

- **`POST /api/uploads/demo?token=`**, `multipart/form-data`, three file parts **in this order**, so the small ones are checked before the large one is read.
- **The link** is one-time, valid for 24 hours, and covers that one demo and nothing else. A retry (Create demo again, or another `create_sprint_demo`) revokes the old link and issues a new one. The token is in the query string, not the path, because the path is logged by the error handler and by ASP.NET's request scope and the query string is not.
- **`chapters`**, at most 1 MiB, JSON: `{ "durationSeconds": 142.6, "chapters": [ { "title": "Intro", "startSeconds": 0 }, { "title": "Checkout v2", "startSeconds": 15.2, "featureKey": "SHIP-101" } ] }`. `durationSeconds` above 0; 1 to 200 chapters; the first starts at 0; starts strictly ascending and each below the duration; titles 1 to 200 characters with no control characters; a `featureKey` must be a live item of the demo's project.
- **`script`**, Markdown, valid UTF-8, not blank, at most 100,000 characters (the limit of the Markdown renderer the Demo tab uses). A 30-minute script is about 30 KB.
- **`video`**, an MP4: the first 12 bytes must carry `ftyp` at offset 4. At most `SHIPLEAN_DEMO_MAX_BYTES`, default 200 MiB, about 80 minutes at 720p. 1080p is untested.
- **All or nothing.** The three files and the sprint's pointers are committed together. If anything fails, the old files and pointers stay, and the same link can be tried again until it expires. Replacing a video frees its space against the work item's attachment total (`SHIPLEAN_ATTACHMENT_MAX_ITEM_BYTES`). The files are kept as `sprint-demo.mp4`, `sprint-demo-script.md` and `sprint-demo-chapters.json` on the BAU item.
- **Answers.** 404 "That upload link is not valid." for every link that isn't live: unknown, expired, spent, replaced by a retry, a person who has left the project, a revoked agent authorization, a disabled user. 400 names the part that is missing, out of order, too big or invalid; after a 400 the link is live again. The route is rate limited per client address like other uploads (60 a minute).
- **Behind a proxy**, `/api/uploads/` must skip sign-in ([auth.md](auth.md#paths-the-proxy-must-skip)), and **the proxy's request body limit must allow the demo size** on that path. nginx's default `client_max_body_size` is 1 MB, which stops a demo; set it to at least `SHIPLEAN_DEMO_MAX_BYTES` there. ShipLean lifts Kestrel's own cap on this route.
- **A proxy's access log can hold the upload link's query string**, and with it the token. The token works once and for 24 hours, so a spent or expired link in a log is dead, but keep access logs away from people who shouldn't upload demos while one is live, or drop the query string from the log format for `/api/uploads/`. ShipLean itself logs no token, puts none in a trace tag or a usage event, and the agent panel never shows the prompt. The prompt, link included, stays in plain text in the agent delivery's body, because the relay doesn't change.

## BAU explained

BAU means **Business As Usual**: background work that just gets done and is never planned into a sprint. ShipLean has one BAU work item type, and sprint demos are its first use.

- **One per sprint.** The first demo of Sprint 24 makes a BAU item called "Sprint 24 demo" in the project, with the next key in the project's numbering (SHIP-640, say). Every retry, from the button, from Automatic demos or from `create_sprint_demo`, reuses it. Because the item can have only one live agent session, two runs can't overlap.
- **It carries the run.** It holds the agent session (an agent session needs a work item) and the demo's three files. The agent panel and comments work on it as on any item, which is where the agent says what it left out.
- **It is made by ShipLean, not by hand.** Making one through the API or the backlog is refused ("A BAU item is made by ShipLean, not by hand."), and so is putting one on a sprint ("A BAU item can't be in a sprint."). It has no parent and no children. It never counts in velocity.
- **It is hidden from lists.** A BAU item never appears in a backlog, a board, a taskboard, search, "what changed", the project's work item list, an item count or any MCP list tool. You reach it only by its key, its id or a link: the Demo tab's band links to it, and `get_work_item` and the item page open it.
- **Its states** are New, Active, Closed and Removed, and show in Project settings › States under their own **BAU** tab, where a project can rename them like any type's.

## Settings

Project settings › **Demos** has its own Save; it isn't part of the page's draft. Project admins change it, members see it read-only.

- **Automatic demos.** Starts a demo when the sprint is closed. It can't be turned on until the project has an agent connection and a repository (a 400 on `automatic` with the reason), and the instance needs a `base_url`, because nobody is making a request to borrow an origin from. If the check fails later while the setting is on, the setting stays and the sprint's Demo tab shows the reason as Blocked. The box can then be turned off, not on.
- **How an agent starts the environment**, up to 4,000 characters. Free text that goes into the prompt as the agent's instructions for starting a local seeded copy. It is never run against production.
- **Demo brand**, next section.
- **YouTube**, [below](#youtube).

## A project's logo and tokens

The logo and the design tokens of a project's slides are real project settings (Demos › Demo brand). With none set, ShipLean's own logo and tokens are used. Both come from a project admin and end up in an agent's browser and prompt on someone else's machine, so both are treated as **untrusted input**: the agent is told they are data, never instructions.

**The logo** is one image, at most 1 MiB. What it is gets decided by its bytes, never by its name or its declared type.

- Accepted: PNG, JPEG or WebP, whose header gives a size of 1 to 4096 pixels on each side; or an SVG, which is cleaned first (scripts, event attributes, foreign objects and every outside reference removed). The cleaned SVG is what is kept.
- Refused, each with a message on the field: a file that isn't one of those ("A logo must be a PNG, JPEG, WebP or SVG image."), over 1 MB, a damaged or cut-short image, or one bigger than 4096 pixels on a side (so a decompression bomb never gets stored). An SVG that can't be made safe, such as one with a DTD, is refused as not an image.
- **Delivery.** The prompt never holds the logo's bytes. For a project's own logo it holds a link, `GET /api/downloads/demo-logo?token=`, valid for 24 hours, for that logo only, and it can be read more than once. It dies with a retry, an expiry, a revoked agent authorization or a disabled user, and it answers 404 once the logo is removed. The default logo is small enough to be inlined. If the stored logo can't be read, the run carries on with ShipLean's logo and the prompt says "The project's logo couldn't be prepared, so ShipLean's is used." The agent puts the logo on a slide only through an `<img>` of a local file, never inline.
- Removing the logo clears the reference; the bytes stay in the blob store, as every blob does.
- Served with `nosniff`, `Cross-Origin-Resource-Policy: same-origin` and a sandboxing CSP.

**The tokens** are text, one `--name: value;` per line, at most 8 KiB and 100 lines. Blank clears them. Slides use seven names, and the panel lists them with ShipLean's values as hints: `--color-bg`, `--color-surface`, `--color-text`, `--color-muted`, `--color-accent`, `--color-accent-fg` and `--font-sans`. A value that is left out keeps ShipLean's.

- A value is exactly one of: a hex colour (`#rgb`, `#rrggbb`, `#rrggbbaa`); `rgb(...)` or `hsl(...)` holding only digits, `.`, `%`, `,`, `/` and spaces; a number with an optional `px`, `rem`, `em` or `%`; or a font stack of up to 8 families, each on an allowlist (`serif`, `sans-serif`, `monospace`, `system-ui`, `ui-serif`, `ui-sans-serif`, `ui-monospace`, `-apple-system`, `Inter`, `Arial`, `Helvetica`, `Helvetica Neue`, `Segoe UI`, `Roboto`, `Open Sans`, `Verdana`, `Tahoma`, `Trebuchet MS`, `Georgia`, `Garamond`, `Times New Roman`, `Times`, `Courier New`, `Courier`, `Consolas`, `Menlo`, `Monaco`). The six `--color-*` names take only a colour and `--font-sans` only a font stack.
- Refused, naming the line: comments, `url(`, `@import`, `expression(`, `<`, `>`, `{`, `}`, `\`, control characters, a name that isn't lower case, a font that isn't on the list, anything over 200 characters. So the tokens hold no free text (a quoted sentence is refused as a font), can't load anything and can't break out of the rule they're written into.
- What is stored is the normalised lines, not what was typed. A valid line with another name is stored and shown, and the panel says it isn't used. **It never enters the prompt.**

## Playback

The Demo tab plays the kept MP4 from ShipLean (`GET /api/attachments/{id}/content` answers `Range` requests for MP4 and WebM, so seeking works). A chapter click seeks the player.

Never link a demo through the attachment page: it renders only Markdown. Links to a demo go to `/p/{key}/sprints/{sprintId}/demo`.

## YouTube

YouTube is optional, and **not proven against Google yet** (see [Not proven yet](#not-proven-yet)). Each project connects its own channel. Without it the Demo tab plays the attachment.

### Setting up the instance

The instance holds one Google OAuth client for YouTube. It is separate from Sign in with Google ([auth.md](auth.md#sign-in-with-google)): the quota and the consent screen belong to the Google project, and the YouTube scope must not touch sign-in. `SHIPLEAN_GOOGLE_*` is never read for this.

- Set `SHIPLEAN_YOUTUBE_CLIENT_ID` and `SHIPLEAN_YOUTUBE_CLIENT_SECRET` (both or neither; one without the other stops startup, naming the missing one), or save them in Instance settings › Integrations, which wins over the environment. The secret is stored protected.
- With neither set, YouTube is **Off**: the project's YouTube panel says so, and every YouTube route answers 404.
- In Google: enable the YouTube Data API v3, set up an OAuth consent screen, add the scope `https://www.googleapis.com/auth/youtube.force-ssl`, and create a **Web application** client whose authorized redirect URI is `<base_url>/api/youtube/callback` (the instance's `base_url`, or the request's origin when it has none). `youtube.upload` alone can't read a video's status or delete one, which the check needs.
- The scope is requested with `access_type=offline&prompt=consent`, so Google issues a refresh token. Videos are uploaded with `selfDeclaredMadeForKids` false, by the resumable protocol.

### Connecting a channel

A project admin goes to Project settings › Demos › YouTube and presses **Connect YouTube**. Google asks for consent and sends the browser back to `/api/youtube/callback`, which always answers with a redirect to the settings page (`?youtube=pending`, `denied` or `error`).

The callback is open to anonymous requests only because it must be; it checks the rest itself. The `state` is sealed by the server and holds the user, the project and a 10-minute expiry. It must open, not be expired, match the signed-in user, and that user must still be an admin of the project. A state that fails any of these redirects to `/?youtube=error` and stores nothing. Then the code is exchanged. No refresh token, or a granted scope without `youtube.force-ssl`, means the grant is revoked and nothing is stored. Otherwise the channel is read, any earlier channel of the project is archived and revoked, and the new one is stored as **pending**.

Behind an identity proxy the callback must **not** be skipped: it needs the person's session.

### The check

Nothing is connected until a check passes. In the panel the person chooses a visibility (Public, Unlisted or Private) and presses **Check and connect**. ShipLean then:

1. uploads a small test video (3 KB, 2 seconds) at the chosen visibility,
2. reads back the video's `status.privacyStatus`,
3. **deletes the test video, always**, whatever happened,
4. saves the channel as **connected** only if the read-back equals the choice.

Changing the visibility of a connected channel runs the check again, and a channel that fails a re-check goes back to pending. A check uses **one of the Google project's 100 uploads a day**; quota is counted per Google project, not per channel or per ShipLean project.

**If the read-back differs, the check is refused** and the channel stays pending with this text:

> YouTube gave the test video unlisted visibility, not the public you chose. The channel or its account settings decide this. Change the channel's setting and check again, choose unlisted, or don't use YouTube.

(The two words are whatever came back and whatever was chosen.) It says nothing more about why, because the API doesn't. The channel or its account settings gave a different visibility than the one chosen. Google's documentation says that an upload with no privacy status set may default to private depending on the creator's account settings. ShipLean always sends one, and whether an account setting can override it is one of the things the spike is for. There are three ways out: change the channel's setting, pick the visibility that came back, or leave YouTube off.

Other failures stay pending and say why: the quota is used up, or access was revoked. If deleting the test video fails after a matching read-back, the channel still connects, and the result names the video id so the person can delete it in YouTube Studio.

Google used to restrict videos uploaded from unverified projects to private viewing. It reversed that on 2 Nov 2026 ([YouTube Data API revision history](https://developers.google.com/youtube/v3/revision_history)), so an unverified project's uploads are no longer forced private. ShipLean doesn't rely on either rule; the check reads what came back.

### Forwarding the demo

The agent always uploads to ShipLean. When a demo arrives and the project has a connected channel, the sprint's YouTube state becomes pending, and a background loop forwards the kept file to YouTube every 30 seconds: title `{project name}: {sprint name} demo`, description the chapters as `m:ss title` lines, at the channel's visibility, then it reads the privacy back. The result is one of:

- **Published.** If the video isn't private, the tab embeds it (from `youtube-nocookie.com`) and links to it; a chapter click seeks the embed. If it came back **private**, the embed can't be watched, so the tab plays ShipLean's file, adds **Watch on YouTube** and says that only the channel owner can watch it there.
- **Failed** (quota, revoked access, a rejected upload, the network). The attachment is untouched and the tab plays it, with the reason and **Retry upload**. A retry sets the state back to pending. A failure sticks until someone retries; it isn't retried by itself.
- **None**, when the channel was disconnected meanwhile or there was never one.

A local seeded instance (`ShipLean.LocalHost`) doesn't run the forwarding loop.

### Disconnecting

**Disconnect** revokes the token at Google first, then always archives the channel and clears the token. If Google doesn't confirm the revocation, the channel is still disconnected, because ShipLean would have no token to try again with, and the panel tells the person to remove ShipLean's access in their Google account. Already published videos stay on YouTube.

## The live spike

Joel runs this once. Nothing on the host that built this feature has proved the check against Google. Do it against a **throwaway Google project and a throwaway channel**.

1. Make a Google Cloud project. Enable YouTube Data API v3 in it. Set up the OAuth consent screen with user type **External** and yourself as a test user (Google Auth Platform: Branding, Audience, Data Access). Add the scope `youtube.force-ssl` under Data Access. Create a **Web application** client with the redirect URI `<base_url>/api/youtube/callback`. While the screen's publishing status is Testing, Google expires a refresh token after 7 days, so expect the connection to stop working after a week.
2. Set `SHIPLEAN_YOUTUBE_CLIENT_ID` and `SHIPLEAN_YOUTUBE_CLIENT_SECRET` on a non-prod instance with a public `base_url`.
3. On a throwaway project, go to Settings › Demos and press **Connect YouTube**, against a throwaway channel. Choose **Unlisted**, and run the check.
4. Record: what `privacyStatus` the read-back gave and how soon; whether the check connected; whether the test video was really deleted (look in YouTube Studio); the quota used (Google Cloud console › APIs › Quotas, the Video Uploads bucket).
5. **Connect the same Google account twice:** press **Connect YouTube** again with the same account and run the check on the second connection. ShipLean exchanges the code, stores the new channel, and then revokes the earlier channel's token. Google's revoke may end the whole grant for that account, which would kill the second connection's token too. If the second check fails as revoked, the earlier token must be revoked **before** the exchange, not after; that is a change to `YouTubeConnect`, so record it as a follow-up.
6. Close a sprint with a connected channel (or deliver a demo) and watch the forward. Then **Revoke access** for ShipLean in the Google account, retry, and see the failure on the Demo tab.
7. Write the result on SHIP-303. **This proves the round trip and the read-back, nothing more.** A mismatch can only be seen if an account setting forces private. If the throwaway channel has one, set it, choose Unlisted again, and record the refusal. Without one, the refusal path stays proven only against the fake and the stub. If a forced setting doesn't show in `privacyStatus` straight away, the check can't catch it, and re-reading after a delay is the likely fix, as a follow-up.

## Not proven yet

- The YouTube check, the forward and the disconnect have been run against a fake and a stub of Google's API, never Google. A stub that behaves as the code expects proves nothing about how Google behaves.
- Whether Google ever shows a forced account setting in `privacyStatus` is unknown. Until the spike, the refusal is exercised by the fake only.
- Whether revoking the earlier token on a second connection from the same Google account ends the new one (step 5).

The UI and the docs say none of this is proven until the result is on SHIP-303.
