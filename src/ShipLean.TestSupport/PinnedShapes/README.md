# Pinned persisted-JSON fixtures

These files are the contract between the database seed (`db/seed`, `SeedIds`) and the C# and TypeScript shapes (`ShipLean.Contracts.Persisted`, `ShipLean.Contracts.Refs`, `api/persisted.ts`, `api/refs.ts`) with their round-trip tests. The work item, board, event, import and criteria code builds on the same shapes. If a file here is wrong, stop and report: never compensate on your side.

| File | What it pins | Persisted in (the schema to confirm) |
|---|---|---|
| `field-ids.json` | The fixed field-id vocabulary (`FieldDefinition[]`) | Not stored: a code constant in C# and TS, pinned to this file by a test |
| `criteria.*.json` | One `CriteriaJson` document each | Inside `board.settings` (swimlanes, style rules); saved backlog filters in `user_preference.json` |
| `board-settings.{stories,features,epics,taskboard}.json` | `BoardSettingsJson` for SHIP's four boards | `board.settings` (one row per project × `board.level`) |
| `field-change.json` | `revision.changes` (`FieldChange[]`) examples, with their revision row | `revision.changes` |
| `import-job-state.json` | `ImportJobState`, `ImportStep`, `ImportJobProgressJson` examples | `import_job.status` (enum) + `import_job.progress` |
| `refs.json` | `UserRef`, `StateRef`, `SprintRef`, `TypeRef` examples, **and the pinned seed ids** | Not stored: DTO pieces |
| `me.json` | `MeDto` (Joel, as `GET /api/me` returns him), `UserDto` in every `UserStatus`, and the `user_preference` values `MeDto` reads | `app_user` + `user_preference` (read model, not stored as one document) |

## Conventions (every file)

- **Property names are camelCase. Enum values are camelCase strings** (`inProgress`, `doneWithErrors`, `blockedBy`, `changesBacklogOrder`). The only exceptions are the criteria operators `=`, `<>`, `>`, `<`, which are written as symbols (`in`, `contains`, `notContains`, `isEmpty`, `isNotEmpty` follow the camelCase rule). C#: `System.Text.Json` with `JsonSerializerDefaults.Web`, enums as camelCase strings, operators mapped with explicit member names (`JsonStringEnumMemberName`). TS: string-literal unions.
- **Nothing is omitted.** Every property is always written; absent values are `null`. Lists are never `null`: empty is `[]`. Serializers must not use `WhenWritingNull`.
- **Ids are lowercase UUID strings**, except type ids (`epic`, `feature`, `story`, `bug`, `task`), field ids, and the ids of things that live only inside a board's JSON (columns, swimlanes, style rules: short strings such as `col-new`).
- **Timestamps** are ISO 8601 UTC with `Z`, second precision (`2026-10-19T09:12:00Z`). Fixture dates are anchored at the design's "today", Sat 2026-10-24. The seed shifts them relative to the run date (decision 11). Round-trip tests use them as they are.
- **Array order is significant** unless a section below says it isn't.
- **Round-trip test:** deserialize each document into the typed shape, serialize it again, and compare with the fixture semantically (`JsonNode.DeepEquals` in C#, deep-equal in TS). Declaring properties in fixture order is recommended but not required.
- In `field-change.json` and `import-job-state.json`, each example has a `row` (table columns, shown for context, **not** JSON) and the persisted document (`changes` / `document`). Only the document is the typed shape.

## Pinned seed ids (the seed uses these values in `SeedIds.cs` / `seedIds.ts`)

Format `KKKKKKKK-0000-4000-80PP-NNNNNNNNNNNN` (valid v4 layout). `KKKKKKKK` = kind, `PP` = project number, `N` = number.

| Kind | Prefix | Numbering | Examples |
|---|---|---|---|
| User | `00000001` | `PP` = `00`; Joel 1, Priya 2, Tom 3, Mei 4, Sam 5, Ana 6 | Joel is the exception: `0199f000-0000-7000-8000-000000000001` (`SeedIds.Users.Joel`); Priya `00000001-0000-4000-8000-000000000002` |
| Project | `00000002` | `PP` = `00`; SHIP 1, PAY 2, MOB 3, DATA 4, OPS 5, PART 6 | SHIP `00000002-0000-4000-8000-000000000001` |
| State | `00000003` | `PP` = project; `N` = type digit (epic 1, feature 2, story 3, bug 4, task 5) then two-digit order | SHIP Bug Triaged `00000003-0000-4000-8001-000000000402` |
| Sprint | `00000004` | `PP` = project; `N` = sprint number | SHIP Sprint 24 `00000004-0000-4000-8001-000000000024` |
| Work item | `00000005` | `PP` = project; `N` = key number | SHIP-141 `00000005-0000-4000-8001-000000000141` |
| Import job | `00000006` | `PP` = `00`; `N` = job number | MOBILE → MOB `00000006-0000-4000-8000-000000000001` |

`refs.json` lists every SHIP state (per type, in state order), every SHIP sprint (19–26), all six users and the five types. Other projects follow the same scheme. Their states and boards aren't pinned here: the seed writes documents in the same shape with their own state ids.

---

## `field-ids.json` — `FieldDefinition[]`

C# `FieldDefinition` records in a static catalogue `FieldIds` (`Contracts/Persisted`); TS `FieldDefinition` and `FIELD_IDS` (`api/persisted.ts`). Both carry the list in this file's order, and a pin test compares each of them with this file.

| Property | JSON type | Null? | Meaning |
|---|---|---|---|
| `id` | string | no | Field id, camelCase. Used in criteria clauses, `extraFields`, `FieldChange.field` and the criteria evaluator's field-value accessor |
| `label` | string | no | Default English label ("Assigned to"; the Features and Epics UI says Owner for the same field) |
| `kind` | `FieldKind` | no | Value type, see the table below |
| `appliesTo` | `WorkItemTypeId[]` | no | Types that have this field |
| `nullable` | bool | no | Whether the value can be empty. List kinds (`tags`, `checklist`, `links`) are never null; empty is `[]` |
| `usedIn` | `FieldUse[]` | no | `criteria` (allowed in a clause), `cardField` (allowed in `extraFields`), `history` (can appear in `FieldChange`) |
| `ops` | `CriteriaOp[]` | no | Operators this field allows in criteria (`[]` when not a criteria field) |
| `values` | `{ value, label }[]` | yes | Fixed value set with display labels (type, stateCategory, priority, severity, originalSystem); `null` = open set |

**Order:** the order of this file is the canonical field order: builders list fields in it, and `FieldChange` lists within one revision follow it.

**`FieldKind` and value typing** (the same in criteria values and `FieldChange.from/to`):

| Kind | JSON value | Fields |
|---|---|---|
| `text` | string | `key`, `title`, `originalSystem`, `originalSystemId`, `originalUrl`, `goal`, `statusLine` (Features only; the last two are history-only) |
| `markdown` | string (markdown source) | `description`, `reproSteps` |
| `number` | integer | `priority` (1–4), `severity` (1–4) |
| `points` | number (decimal; integers in practice) | `points` |
| `user` | user UUID; in criteria also the literal `"@me"` (the viewer) | `assignee`, `createdBy`, `changedBy` |
| `state` | state UUID | `state` |
| `stateCategory` | `proposed` \| `inProgress` \| `resolved` \| `completed` \| `removed` | `stateCategory` |
| `type` | `epic` \| `feature` \| `story` \| `bug` \| `task` | `type` |
| `sprint` | sprint UUID | `sprint` |
| `workItem` | work item UUID | `id`, `parent`, `epic` (the root Epic above a Feature; derived, never in history) |
| `tags` | criteria: one tag string; `FieldChange`: string[] | `tags` |
| `date` | ISO UTC timestamp | `createdAt`, `changedAt`, `statusVerifiedAt` (a Feature's, history-only) |
| `checklist` | `{ text: string, done: bool }[]` | `acceptanceCriteria` |
| `links` | `LinkValue[]` (see `field-change.json`) | `links` |

---

## `criteria.*.json` — `CriteriaJson`

C# `CriteriaJson`, `CriteriaClause`, enums `CriteriaMatch`, `CriteriaOp` (`Contracts/Persisted`); TS the same names plus `CriteriaValue`.

| Property | JSON type | Null? | Meaning |
|---|---|---|---|
| `match` | `all` \| `any` | no | AND or OR across the clauses. One group only, no nesting (the design draws one group) |
| `clauses` | `CriteriaClause[]` | no | **At least one.** "No criteria" is a `null` CriteriaJson, never an empty list |
| `clauses[].field` | string | no | A field id whose `usedIn` has `criteria` |
| `clauses[].op` | `CriteriaOp` | no | `=`, `<>`, `>`, `<`, `in`, `contains`, `notContains`, `isEmpty`, `isNotEmpty`; must be in the field's `ops` |
| `clauses[].value` | per kind | see below | C# `JsonElement`, validated against the field's kind by the criteria engine; TS `CriteriaValue = string \| number \| (string \| number \| null)[] \| null` |

**Operator semantics (the criteria engine):**
- `=`, `<>`: a scalar of the field's kind, never `null`. `<>` matches an empty field.
- `>`, `<`: number, points or date only. An empty field never matches.
- `in`: a non-empty array of scalars. A `null` element matches an empty field (how a people filter with **Unassigned** is written).
- `contains`, `notContains`: on `text`, a substring; on `tags`, one whole tag.
- `isEmpty`, `isNotEmpty`: `value` is `null`. For `tags`, empty means no tags.
- All string comparisons (text, tags) are case-insensitive (ordinal ignore case).
- An unknown field, or an op the field doesn't allow, is a `validation` error on write. It's never ignored.

| File | Shows |
|---|---|
| `criteria.expedite.json` | **Stories board Expedite lane: `Priority = 1`**, and the Taskboard's Priority 1 rule |
| `criteria.blocked.json` | **Blocked style rule: tag contains Blocked** |
| `criteria.spike.json` | **Spike style rule: tag contains Spike** |
| `criteria.customer-bugs.json` | `all` with two clauses (board-settings.html's Customer bugs lane; not seeded) |
| `criteria.waiting-on-review.json` | `stateCategory` and `"@me"` (Taskboard rule, disabled in the seed) |
| `criteria.not-equal.json` | `<>` on type, state id and user |
| `criteria.in.json` | `in` with users (+ `null` = Unassigned), sprints and numbers |
| `criteria.empty.json` | `isEmpty` / `isNotEmpty` |
| `criteria.numeric.json` | `>` / `<` on points, priority and a date |
| `criteria.text.json` | `contains` / `notContains` on text and tags |
| `criteria.any.json` | `match: any` |

**Default lane:** it has no criteria. Its swimlane has `kind: "default"` and `criteria: null` (see `board-settings.stories.json`).

---

## `board-settings.*.json` — `BoardSettingsJson`

One shape for the three level boards and the Taskboard. C# `BoardSettingsJson`, `BoardColumnJson`, `ColumnStateMapping`, `SwimlaneJson`, `CardFieldSettingJson`, `StyleRuleJson`, `TagColourJson`, `CardAnnotationsJson`, enums `SwimlaneKind`, `Swatch`, `CardReordering`; TS the same names. The level isn't inside the JSON: it's the `board.level` column (`epics` \| `features` \| `stories` \| `taskboard`). The seven top-level properties are the **sections**: the store's merge rule and `mergedSections[]` use these exact names.

| Property | JSON type | Null? | Meaning |
|---|---|---|---|
| `columns` | `BoardColumnJson[]` | no | Left to right. **Taskboard: always `[]`** (its columns are the Task states in state order, minus Removed) |
| `columns[].id` | string | no | Unique within the board, never reused or changed. `work_item.board_column_id` holds it, so that column is `text` |
| `columns[].name` | string | no | Header text |
| `columns[].wipLimit` | int | yes | `null` = no limit. Counts both halves of a split column across every lane. Always `null` on the first and last column |
| `columns[].split` | bool | no | Doing/Done halves with the same state (`work_item.board_column_done`). Always `false` on the first and last column |
| `columns[].definitionOfDone` | string | no | Markdown; `""` = none (no ⓘ) |
| `columns[].stateMappings` | `{ type, stateId }[]` | no | Exactly one entry per type on the board, in the order story, bug (Stories), feature (Features), epic (Epics). `stateId` is a state of that type, not in the Removed category. Two columns may share a state; a state set elsewhere moves the card to the first column mapped to it |
| `swimlanes` | `SwimlaneJson[]` | no | Top to bottom. Exactly one `default` lane on level boards. **Taskboard: always `[]`** |
| `swimlanes[].id` | string | no | Like column ids; `work_item.swimlane_id` holds it (`text`) |
| `swimlanes[].name` | string | no | |
| `swimlanes[].colour` | `Swatch` | yes | Lane dot; `null` = none |
| `swimlanes[].kind` | `criteria` \| `manual` \| `default` | no | Criteria lanes are checked top-down and **the first match wins**; otherwise a card stays in the manual lane it was dropped in, else it goes to the default lane |
| `swimlanes[].criteria` | `CriteriaJson` | yes | Non-null only when `kind` is `criteria` |
| `cardFields` | `CardFieldSettingJson[]` | no | One per type shown: Stories story, bug; Features feature; Epics epic; Taskboard task (cards), then story, bug (row headers) |
| `cardFields[].type` | `WorkItemTypeId` | no | |
| `cardFields[].showKey` / `showAssignee` / `showTags` / `showPoints` / `showEmptyFields` | bool | no | Core field toggles |
| `cardFields[].extraFields` | string[] | no | Display order, at most 10, distinct, field ids with `cardField` in `usedIn` that apply to the type |
| `styleRules` | `StyleRuleJson[]` | no | Ordered. **The winning rule is the first enabled rule whose criteria match**; disabled rules are skipped |
| `styleRules[].id` / `name` | string | no | |
| `styleRules[].enabled` | bool | no | |
| `styleRules[].colour` | `Swatch` | yes | Card tint and left bar; `null` = text style only |
| `styleRules[].bold` / `italic` / `underline` | bool | no | Title style |
| `styleRules[].criteria` | `CriteriaJson` | no | |
| `tagColours` | `{ tag, colour }[]` | no | One entry per coloured tag (case-insensitive unique); an uncoloured tag has no entry. Order isn't significant (the pane sorts by use); writers keep it. The backlog at the same level borrows these colours |
| `annotations` | `{ tasks, stories, bugs, features }` (bool each) | no | Child roll-ups on cards. Seeded: Stories `tasks`; Features and Epics `stories`, `bugs`, `features`; Taskboard `tasks` (the ☑ on row headers) |
| `cardReordering` | `changesBacklogOrder` \| `keepsBacklogOrder` | yes | Whether a drag within a column writes `backlog_order`. `null` only on the Taskboard (it has no such setting) |

**`Swatch`** (colour names, never CSS): `danger`, `warn`, `ok`, `info`, `accent`, `muted`, `purple` → `--color-<name>`; `--color-purple` is the label colour token added for the Spike style.

**Not stored here** (per-user `user_preference`, or fixed): compact/full cards, swimlanes on/off, Features lanes None / By Epic (generated from Epics), Done shows the 4 most recent per lane (decision 5, a constant), filter bars, Taskboard Group by.

**Seeded as drawn:** Stories = `board.html` (split Ready WIP 8, WIP 5/2, DoD, Bug `Triaged` → Ready, Expedite `Priority = 1` above Default lane, Blocked/Spike rules, tag colours) with card fields from `board-settings.html`; Features and Epics = `features-board.html` default columns; Taskboard = `taskboard.html` settings.

---

## `field-change.json` — `revision.changes` (`FieldChange[]`)

C# `FieldChange`, `ChecklistItem`, `LinkValue`, enum `LinkType`; TS the same names. The persisted value is the `changes` array of each revision.

| Property | JSON type | Null? | Meaning |
|---|---|---|---|
| `field` | string | no | A field id with `history` in `usedIn` |
| `from` / `to` | per kind | yes | Typed as in the kind table (C# `JsonElement`). `null` = empty; list kinds use `[]` |
| `fromLabel` / `toLabel` | string | yes | **Stored snapshot labels** for kinds `user` (name), `state` (name), `sprint` (name), `workItem` (title), taken at write time so history survives renames and imports. `null` when the value is `null` and for every other kind (those are formatted from the value: severity via `field-ids.json` `values`) |

`LinkValue`: `{ type: predecessor | successor | blockedBy | related | duplicate | url, workItemId: uuid?, key: string?, url: string?, title: string? }`. Item links set `workItemId` + `key` (keys are immutable); `url` links set `url` (+ optional `title`) and leave the other two `null`.

**Rules:**
- History is field changes only (decision 7). Comments, attachments, tasks and board moves write no revision (a move's state change does).
- One save = one revision. `changes` holds only the fields that changed, in `field-ids.json` order.
- **Revision 1** records every non-empty field set at creation, `from: null` (the History panel shows it as "Created as User Story in New"). Velocity and burndown read state, points and sprint from the trail, so they need the initial values.
- Descriptions and repro steps store the full markdown before and after. The panel shows "edited".

**Revision row columns** (schema): `work_item_id`, `rev`, `changed_by` (null = the import itself), `changed_at`, **`original_system`** (`jira` when the revision replays the original system's changelog: the panel shows **from Jira**, by the mapped user), **`import_job_id`** (the job that wrote it; with `changed_by` null the panel shows **Jira import**, as on a re-run update), `changes`.

---

## `import-job-state.json` — `ImportJobState`, `ImportStep`, `ImportJobProgressJson`

C# enums `ImportJobState`, `ImportStep`, records `ImportJobProgressJson`, `ImportCounts`, `ImportEntityCount`, `ImportRerunResult`; TS the same names.

**`ImportJobState`** (`import_job.status`, and `importProgress.state` in the project events): `queued` (Start pressed, waiting on the hosted queue) → `running` → `done` | `doneWithErrors` (finished, item errors in `import_error`) | `stopped` (Stop after this batch) | `failed` (the job itself couldn't go on). `doneWithErrors` becomes `done` once every error is retried or skipped. A re-run puts the same row back to `queued` with `rerun: true`.

**`ImportStep`**, in run order: `project`, `states`, `users`, `sprints`, `issues`, `parents`, `links`, `comments`, `attachments`, `revisions`, `finishing`.

**`ImportJobProgressJson`** (`import_job.progress`):

| Property | JSON type | Null? | Meaning |
|---|---|---|---|
| `percent` | int 0–100 | no | Overall progress (`importProgress.percent`) |
| `step` | `ImportStep` | yes | The step running, or the last one run; `null` while queued |
| `message` | string | no | The latest log line (`importProgress.message`); the full log is `import_log` |
| `rerun` | bool | no | This run matches existing items on original system + id |
| `counts` | `{ sprints, issues, comments, attachments, revisions }`, each `{ done, total, failed }` (int) | no | Per-entity progress bars; `failed` = rows in `import_error` for that entity |
| `usersMatched` / `usersCreated` / `statesCreated` | int | no | From the mapping, shown in the log and summary |
| `rerunResult` | `{ updated, unchanged, added }` (int) | yes | Re-runs only, once finished |

`importProgress { jobId, percent, state, message }` = `row.id`, `document.percent`, `row.status`, `document.message`. Started/finished times, the Jira key and the target project are columns. `mobFinished` is the seed's finished job (`Imported → MOB`); the two PLATFORM examples are illustrations, not seeded.

---

## `refs.json` — `UserRef`, `StateRef`, `SprintRef`, `TypeRef`

Read-side DTO pieces (C# `Contracts/Refs`, TS `api/refs.ts`), never stored inside persisted JSON (persisted JSON stores bare ids, plus snapshot labels in `FieldChange`). The file groups `stateRefs` by type for the seed's benefit; the DTO is the element.

| Type | Property | JSON type | Null? | Meaning |
|---|---|---|---|---|
| `UserRef` | `id` | uuid | no | |
| | `name` | string | no | Display name |
| | `initials` | string | no | 1–2 uppercase letters |
| | `avatarColour` | string `#rrggbb` (lowercase) | no | Identity colour, the same in both themes |
| | `photoUrl` | string | yes | Server-built URL of the avatar photo, `/api/users/{id}/photo?v={photo_version}`; `photo_version` is a counter bumped on every upload, replace or remove (a cache buster, no history); `null` = none (initials on the colour) |
| `StateRef` | `id` | uuid | no | |
| | `name` | string | no | |
| | `category` | `StateCategory` | no | `proposed` \| `inProgress` \| `resolved` \| `completed` \| `removed` |
| `SprintRef` | `id` | uuid | no | |
| | `name` | string | no | `Sprint 24` |
| | `status` | `past` \| `current` \| `future` | no | Computed by the server from the dates (the SPA has no clock); dates and goal live on `SprintDto` |
| `TypeRef` | `id` | `WorkItemTypeId` | no | `epic` \| `feature` \| `story` \| `bug` \| `task` |
| | `name` | string | no | `Epic`, `Feature`, `User Story`, `Bug`, `Task` (icon letter and colour come from the id, client-side) |

## `me.json` — `MeDto`, `UserDto`, `UserStatus`, preference values

`me` is `GET /api/me` for Joel on the seed. `users` are `UserDto` rows, one per status (Priya and the accounts the seed adds for the other statuses). `preferences` are `user_preference` rows as `{key, json}`: `json` is the column's jsonb value.

| Type | Property | JSON type | Null? | Meaning |
|---|---|---|---|---|
| `UserDto` | `id` | uuid | no | |
| | `name`, `email` | string | no | `email` is the full sign-in email |
| | `initials`, `avatarColour`, `photoUrl` | as `UserRef` | `photoUrl` only | Same rules as `UserRef` (a `UserDto` carries every `UserRef` field) |
| | `isInstanceAdmin` | boolean | no | `app_user.is_admin` |
| | `status` | `UserStatus` | no | Derived, see below |
| `MeDto` | every `UserDto` property | | | The signed-in user |
| | `hasProjects` | boolean | no | A live `project_member` row exists (`archived_at IS NULL`) |
| | `canCreateProjects` | boolean | no | `instance_settings.project_creation = 'everyone'`, or the user is an instance admin |
| | `theme` | `os` \| `light` \| `dark` | no | The `theme` preference; `os` when there's no row (the page follows the OS) |
| | `density` | `compact` \| `comfortable` | no | The `density` preference; `compact` when there's no row |

**`UserStatus`** is derived from `app_user`, first match wins: `disabled` (`disabled_at` set) → `noPassword` (`password_hash` null: created by a Jira import or by an admin before sign-in was set up) → `locked` (`locked_until` later than now) → `oneTimePassword` (`must_change_password`) → `active`.

**Preference keys** (`user_preference.key`; `json` is the value itself, not wrapped): `theme` (`"os"`/`"light"`/`"dark"`), `density` (`"compact"`/`"comfortable"`), `recentProjects` (project ids, most recent first), and `view:<screen>` keys owned by the screen that writes them. Reset saved views sets every `view:*` row's `json` to JSON `null`; rows are never deleted. Reading a key with no row, or with JSON `null`, means the default.

---

## Open points

1. **State references are state ids, not name + type.** Renaming a state then needs no JSON rewrite. The ids are pinned above and in `refs.json`, so the seed must use them.
2. **All entity ids are assumed `uuid`.** If the schema uses another key type, only the id strings change.
3. **Column and swimlane ids live in the JSON**, so `work_item.board_column_id` and `swimlane_id` are `text` with no FK. How new ids are generated is left to the board settings store and its dialog (opaque, unique per board).
4. **`board.html` and `board-settings.html` disagree about the Stories board.** I seeded `board.html` (the plan says "as drawn"): two lanes (Expedite `warn`, Default lane), the Blocked and Spike rules, and its tag colours. I didn't seed board-settings.html's Customer bugs / Standard / Tech debt lanes, its Expedite bold rule or its tag palette. Card fields come from board-settings.html, because board.html has none.
5. **Colours are swatch names.** `board.html`'s `--chart-committed` (Payments) → `accent`; `--rule-spike` → `purple`. The Taskboard keeps Spike `info`, as drawn.
6. **Operators:** the design draws `=`, `≠` (stored `<>`), contains, not contains, `>`, `<` and All/Any. `in`, `isEmpty` and `isNotEmpty` were added because the brief asked for them, and the filters' Unassigned / Backlog choices need "empty". There's no `>=`/`<=` and no nesting.
7. **Board filter bars aren't pinned as criteria.** The keyword spans title, key and tags, and no single field covers that. If the board stores them as criteria: people → `assignee in [..., null]`, Epic → `epic =`.
8. **All string comparisons are case-insensitive.** The design doesn't say.
9. **`blocked` isn't a field.** Blocked is a tag (and the `blockedBy` link type).
10. **Added beyond the brief's kinds:** `markdown`, `checklist`, `links`. Added fields: `severity`, `description`, `acceptanceCriteria`, `reproSteps`, `links`, `createdBy`, `changedBy`, `epic` (derived, for the Features filter) and `originalUrl`. `id` is in the vocabulary, but no builder offers it.
11. **History labels are stored snapshots** for user/state/sprint/work item and derived for everything else.
12. **Revision 1 stores the initial values.** The design shows only "Created as …" for it.
13. **The import marker is on the revision row** (`original_system`, `import_job_id`, both new columns), not in `changes`.
14. **SHIP-153 doesn't match the seed's import jobs.** work-item.html has it imported from PLATFORM-412 by "import job #3", but the seed's only job is MOBILE → MOB, and a PLATFORM import would key items `PLAT-`. The fixture uses job id `…0003`; the seed either adds that job or seeds SHIP-153 without import history.
15. **`ImportJobState.failed` isn't drawn.** It's there so a job can end when the job itself breaks. The MOB counts other than 1,284 issues are invented. `import_job.progress` is my column name.
16. **SprintRef = `{id, name, status}`, TypeRef = `{id, name}`.**
17. **Invented values:** Joel's name, initials and colour (`Joel Dickson`, `JD`, `#5b6b8c`), and Priya's photo (the seed gives her a blob ref and `photo_version` 1).
18. **Points are decimal; priority is required; severity is nullable.**
19. **On the Taskboard,** `cardReordering` is `null` and `columns`/`swimlanes` are `[]`. Its row-header toggles map to `annotations.tasks` and `extraFields: ["state"]`; "Parent story" maps to `extraFields: ["parent"]`.
