Seed data, applied last (in file-name order) by test fixtures and `scripts/db-reset.sh`. The app itself never applies the seed.

The seed is the world drawn in `design/screens/shared.js`. Ids are deterministic and all listed in
`src/ShipLean.TestSupport/SeedIds.cs` (pinned to `src/ShipLean.Client/src/seedIds.ts`); the scheme is in the pinned shapes README.

| File | Content |
|---|---|
| `0001_instance_and_users.sql` | Instance settings; Joel (id `TemporaryUser.JoelId`), Priya, Tom, Mei, Sam, Ana. Joel and Tom are instance admins, Ana must change her password. No avatar photos. Passwords: `SeedIds.Passwords` (hashes are ASP.NET Core Identity v3) |
| `0002_projects_members_states.sql` | SHIP, PAY, MOB, DATA, OPS, PART; members (Joel is in every project but PART, admin of SHIP and MOB); SHIP's states from `refs.json` (custom Bug `Triaged`, Task `In review`); default states for the others |
| `0003_sprints_and_capacity.sql` | SHIP Sprints 19-26 and the capacity rows (none for Joel) |
| `0004_boards.sql` | SHIP's four boards (the pinned `board-settings.*.json` verbatim); the other projects get default boards generated from their states |
| `0005_ship_work_items.sql` | SHIP's epics, features, stories, bugs and tasks (SHIP-90..183), each with revision 1 generated from its row |
| `0006_import_job.sql` | MOB's Jira connection (no token) and the finished import `MOBILE -> MOB` |
| `0007_accounts_and_first_run_projects.sql` | Users in every account status, an active user in no project, the Jira import attribution user, ARCH (no active admin) and EMPT (empty); see below |
| `0008_metric_history.sql` | History items and revisions behind Sprints 19-23's committed/completed points and Sprint 24's burndown (see "Metric history") |
| `0009_comments.sql` | Two comments by Joel on SHIP-90 (`SeedIds.Comments`): routes keyed on a comment id need one; the second is for archive tests |
| `0010_attachments.sql` | Two text attachments by Joel on SHIP-90 (`SeedIds.Attachments`); rows only, their bytes are in `SeedAttachmentBlobs` (TestSupport) for tests that pin an in-memory blob store |
| `0011_feature_editor_links.sql` | Two URL links by Joel on SHIP-101, a Feature (`SeedIds.Links`): a GitHub pull request with a stored preview (its image and favicon blobs are in `SeedAttachmentBlobs`) and a page link for archive tests; routes keyed on a link id need them |

## Accounts and first-run projects (`0007`)

Status is derived as the pinned shapes README says (`disabled`, then `noPassword`, `locked`, `oneTimePassword`, `active`). Ids are pinned in `me.json`.

| User | Status | Why it exists | Projects |
|---|---|---|---|
| Ravi Menon | `oneTimePassword` | Must change the password at first sign-in (`ravi-dev-password`) | PAY |
| Dana Whitfield | `noPassword` | Created by the MOB import (`created_at` = the import job's start); no password yet | MOB, PART |
| Lee Park | `disabled` | Disabled 55 days ago; the only admin of ARCH | ARCH (admin), OPS |
| Jo Becker | `locked` | 10 failed sign-ins, locked until the end of tomorrow (UTC), not `now() + n`, because the test-database template is cached per UTC day | MOB |
| Kai Moreno | `active` | An active user in no project (the first-run state) | none |
| Jira import | `disabled` | Attribution user for imported history whose author cannot be matched; never signs in, never counts as an admin | none |

| Project | Content |
|---|---|
| `ARCH` Legacy Billing | Lee Park is its only admin (disabled, so no active admin), Mei a member. Default states and boards, no sprints, one Feature and four Stories (ARCH-1..5, next number 6), each with revision 1 |
| `EMPT` Research | Joel admin, Priya member. Default states and boards; no work items, no sprints, next number 1 |

## Relative dates

Nothing is a date literal. `today` is the UTC date the seed runs (`(now() AT TIME ZONE 'utc')::date`, never `current_date`).
`anchor` is the latest working day (Mon-Fri) on or before `today`. Sprint 24 starts 3 working days before `anchor` and ends 13 days
later (two calendar weeks, so its start weekday varies with the run date). Sprint N starts `Sprint 24 start + (N - 24) * 14 days`.
Every other timestamp (item creation, closed times, the import job) is derived from the sprint dates or `today`.
Sprint status is not stored; the server derives it. Sprints 19-23 have `rollover_status = decided`, 24-26 null.

## Metric history (`0008`)

Semantics the script seeds to (the metric slice is pinned to these numbers; every figure derives from `revision` rows alone):

- **Committed** = story points of Stories and Bugs whose sprint was this sprint at the sprint's start (start date 00:00 UTC).
- **Completed** = points of Stories and Bugs whose state category became Completed while their sprint was this sprint, between its start and end, whenever they were added.
- Task points never count (SHIP-45 is a 3-point Task closed in Sprint 22).
- **Burndown** = remaining (committed + added - completed) points of Stories and Bugs at the end of each working day.

| Sprint | Committed | Completed | Carried to the next sprint | Added after the start |
|---|---|---|---|---|
| 19 | 18 | 16 | SHIP-34 (2) | none |
| 20 | 22 | 19 | SHIP-39 (5) | SHIP-40 (2, day 4) |
| 21 | 20 | 20 | none | none |
| 22 | 24 | 22 | SHIP-49 (5) | SHIP-50 (3, day 3) |
| 23 | 23 | 21 | SHIP-138 (2) | none |

Sprint 24 commits 21 and burns down 21 -> 18 -> 16 over working days 1-3 (SHIP-150 closes on day 2, SHIP-138 on day 3 at 15:00 UTC).

The history Stories and Bugs (SHIP-30..52) have **no parent**, so the Features' roll-ups stay as the design draws them (SHIP-45, a Task, sits under SHIP-46). They are Closed in the Stories board's Done column, so that column holds more cards than `board.html` draws: tests about "Show N older" count from the seed, not the design.

History items are SHIP-30..SHIP-52 (new, Closed, key numbers below 90, `next_item_number` unchanged); SHIP-118, 120, 124, 126, 131, 138 and 150
(existing) get a real history: their revision 1 is rewritten as the creation (state New, no sprint) and later revisions are appended.
A history item has: rev 1 `created` at `created_at`; a sprint revision (11:00 UTC on the last working day before the sprint starts, or
10:00 UTC on the day it was added); an Active revision; for a leftover, a sprint move at 17:00 UTC on the last working day before the next
sprint's start date; a Closed revision inside the sprint it closed in. All times are working days, computed from the sprint dates. SHIP-138
was carried from Sprint 23 (that move is a real revision) and now closes on Sprint 24's working day 3.

## What is not here

`board.html`'s page-local extras (SHIP-146, 148, 149, 152, 157) are not seeded. Revisions are keyed `(work_item_id, rev)`; nothing may
assume rev 1 is the latest. SHIP's other items still have a single revision.

## Editing the seed

The seed SQL is edited directly; there is no generator in the repo. A new password hash comes from
`new PasswordHasher<object>().HashPassword(null!, password)` (`Microsoft.AspNetCore.Identity`), pasted into the SQL, with the plaintext
added to `SeedIds.Passwords`.
