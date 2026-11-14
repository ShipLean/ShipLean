# Task changes (SHIP-1460)

A Task or Bug shows the commits it was done in. **Review changes** opens their diff inside ShipLean, where people comment on lines. **Request changes** sends the open comments back with the work item to whichever agent picks it up next. The page is for desktop only: under 900px it isn't there, and the phone app never shows it.

Joel asked for four things: desktop only, Tasks only (D3 later added Bugs), a data model that knows which commits and repositories a work item links to, and instructions telling agents to send them when they resolve the work. He settled all 16 decisions in a Lavish review on 14 Nov 2026. Twelve kept the recommendation; **D3, D4, D6 and D12** changed. Nothing here is built.

## Files

| Mock | States |
|---|---|
| `screens/task-changes.html?view=task` | The Task page (SHIP-171) with its Changes section. `&item=bug` shows a Bug (SHIP-153) with its own commits. `&state=empty` shows no commits yet, with the Add commit form open |
| `screens/task-changes.html?view=review` (the default) | The Changes page, `/p/SHIP/items/SHIP-171/changes`. `&state=compose\|commits\|split\|request\|approve\|outdated\|noaccess` |

Images: `screens/task-changes*.png` (1440 × 900) in light and dark. The variants are in `screenshots.sh`. `screens/task-changes-phone-*.png` (390 × 844) show the narrow-screen states; `screenshots.sh` can't shoot that narrow, so these were taken with headless Chrome at `--window-size=390,844`.

## Screens

**Task or Bug page.** A **Changes** section sits between Description and Discussion. It has one row per commit: sha, subject, repository, who, when and +/−. It also shows the totals (commits, files, +/−), how many comments are open, how many files you've viewed, and a **Review changes** button. Commits leave the Links list. The pull request stays there as a card. With no commits, the section says what agents send, and **+ Add commit** takes a repository and a sha or commit URL as free text.

**Changes page.**
- It is full width, with three columns. Files are on the left: status letter, open comments, Viewed. The diff is in the middle, unified or split; a file collapses once it's marked Viewed. Comments are on the right, under Open and All tabs.
- Hover a line number and press **+** to comment. The comment is tied to that line and to the newest commit on screen.
- The commit picker offers **All**, or one commit at a time. All is one combined diff when the commits follow one another. When another task's commits sit between them, All lists each commit's diff in turn.
- **Approve** moves the item to its first Completed state and records the approved commits in History. It warns when comments are still open, and they stay open.
- **Request changes · n** sends the item to its first Proposed state, unassigned. The open comments go into its brief. An optional note goes on the item as a comment, and a box keeps the assignee and Active instead.
- **A new commit since you looked:** a banner appears, the file it changed loses Viewed, and a comment on a changed line becomes **Outdated**, showing the old line struck through.
- **No diff:** when the token is refused, or the commit no longer exists because it was squashed or force-pushed away, the page shows no diff. Each commit still opens on GitHub or GitLab.

**Under 900px.** The work item has no Changes section; the rest of the page is unchanged. A shared `/changes` link shows one line, "Changes need a wider screen", and a link back. The phone app's view of the work item leaves commits out.

| Screen | Light | Dark |
|---|---|---|
| Task page | ![Task page with Changes, light](screens/task-changes-task.light.png) | ![Task page with Changes, dark](screens/task-changes-task.dark.png) |
| Bug page | ![Bug with its own commits, light](screens/task-changes-bug.light.png) | ![Bug with its own commits, dark](screens/task-changes-bug.dark.png) |
| No commits · Add commit | ![No commits yet, light](screens/task-changes-empty.light.png) | ![No commits yet, dark](screens/task-changes-empty.dark.png) |
| Changes | ![Changes page, light](screens/task-changes.light.png) | ![Changes page, dark](screens/task-changes.dark.png) |
| Writing a comment | ![Writing a comment, light](screens/task-changes-compose.light.png) | ![Writing a comment, dark](screens/task-changes-compose.dark.png) |
| Commit picker | ![Commit picker, light](screens/task-changes-commits.light.png) | ![Commit picker, dark](screens/task-changes-commits.dark.png) |
| Split | ![Split layout, light](screens/task-changes-split.light.png) | ![Split layout, dark](screens/task-changes-split.dark.png) |
| Request changes | ![Request changes, light](screens/task-changes-request.light.png) | ![Request changes, dark](screens/task-changes-request.dark.png) |
| Approve | ![Approve, light](screens/task-changes-approve.light.png) | ![Approve, dark](screens/task-changes-approve.dark.png) |
| New commit · outdated | ![New commit since you looked, light](screens/task-changes-outdated.light.png) | ![New commit since you looked, dark](screens/task-changes-outdated.dark.png) |
| Can't read the diff | ![No diff, light](screens/task-changes-noaccess.light.png) | ![No diff, dark](screens/task-changes-noaccess.dark.png) |
| Phone · Task page (390) | ![Task page on a phone, light](screens/task-changes-phone-task.light.png) | ![Task page on a phone, dark](screens/task-changes-phone-task.dark.png) |
| Phone · /changes link (390) | ![Changes link on a phone, light](screens/task-changes-phone-changes.light.png) | ![Changes link on a phone, dark](screens/task-changes-phone-changes.dark.png) |

## Data model

One additive migration. Today `report_task` saves each commit only as a URL link card titled `commit <sha7> <subject>` (`ReportTools.AddLinksAsync`). That card has no repository and a 7-character sha, so nothing can fetch a diff from it.

```sql
CREATE TABLE code_repository (   -- instance-wide, made the first time a commit names it (D4)
  id          uuid PRIMARY KEY,
  provider    text NOT NULL CHECK (provider IN ('github', 'gitlab')),
  host        text NOT NULL,   -- github.com, gitlab.example.com (lower case)
  path        text NOT NULL,   -- acme/storefront (lower case)
  created_at  timestamptz NOT NULL DEFAULT now(),
  UNIQUE (provider, host, path)
);

CREATE TABLE work_item_commit (  -- a commit on a Task or Bug, in order
  id             uuid PRIMARY KEY,
  work_item_id   uuid NOT NULL REFERENCES work_item (id),
  repository_id  uuid NOT NULL REFERENCES code_repository (id),
  sha            text NOT NULL CHECK (sha ~ '^([0-9a-f]{40}|[0-9a-f]{64})$'),
  position       int  NOT NULL,
  added_via      text NOT NULL CHECK (added_via IN ('report_task', 'add_commits', 'form')),
  added_by       uuid REFERENCES app_user (id),
  added_by_agent text,
  created_at     timestamptz NOT NULL DEFAULT now(),
  archived_at    timestamptz
);
-- unique (work_item_id, repository_id, sha) WHERE archived_at IS NULL

CREATE TABLE code_commit (       -- what the commit is, read once in the background; no patch (D6)
  repository_id uuid NOT NULL REFERENCES code_repository (id),
  sha           text NOT NULL,
  parent_shas   text[] NOT NULL,
  subject       text NOT NULL,
  author_name   text NOT NULL,
  authored_at   timestamptz NOT NULL,
  additions     int NOT NULL,
  deletions     int NOT NULL,
  files_changed int NOT NULL,
  read_at       timestamptz NOT NULL,
  PRIMARY KEY (repository_id, sha)
);

CREATE TABLE code_comment (      -- a comment on a line, or a reply
  id            uuid PRIMARY KEY,
  work_item_id  uuid NOT NULL REFERENCES work_item (id),
  parent_id     uuid REFERENCES code_comment (id),
  repository_id uuid REFERENCES code_repository (id),
  sha           text,            -- the newest commit on screen when it was written
  path          text,
  side          text CHECK (side IN ('old', 'new')),
  line          int,
  start_line    int,             -- a range
  line_text     text,            -- the line as it was, for an outdated comment
  body          text NOT NULL,
  author_id     uuid REFERENCES app_user (id),
  author_agent  text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  resolved_at   timestamptz,
  resolved_by   uuid REFERENCES app_user (id),
  outdated_at   timestamptz,     -- a later commit changed the line
  sent_back_at  timestamptz,     -- went back with Request changes
  archived_at   timestamptz
);
```

- Commits go only on a Task or a Bug. The command checks the type.
- The instance's GitHub or GitLab token must be able to read the repository when the commit is added. There is no allow-list (D4), so any project member can then read that repository's diffs. Scope the token to the repositories everyone may read.
- The diff is fetched when someone opens Changes and cached for a day: in Redis when it's configured, otherwise in memory (D6). A commit that has since been squashed or force-pushed away shows as *no longer on GitHub*.
- Viewed is kept in the browser, in localStorage per work item, file and head sha (D12). It has no table.
- Adding commits writes one revision; History shows "Commits +3 (acme/storefront)". Deletes are soft (`archived_at`), as everywhere else.
- Old `report_task` commit cards move into `work_item_commit` when the token can read their repository (D11).

## Agent contract

- **`report_task`** (tasks of a plan): each commit gains `repo`, and `url` becomes optional. The old shape (short sha + url) still works: ShipLean takes the repository from the URL and resolves the full sha. The new field `answered_comments: [{ id, reply }]` posts each reply on its thread and resolves the thread.
- **`add_commits(item, commits: [{ repo, sha }])`** (new; any Task or Bug; at most 25 a call) adds commits and never removes any. A commit the item already has is skipped. Errors:
  - `not_task_or_bug`: names a Story's tasks.
  - `repo_unreadable`: "ShipLean's GitHub token can't read acme/x (404)".
  - `bad_sha`: "Send the full sha: git rev-parse HEAD".
- **`get_work_item` and `get_brief`** on a Task or Bug gain two fields:
  - `commits: [{ repo, sha, subject, additions, deletions }]`
  - `open_code_comments: [{ id, path, side, line, sha, line_text, body, by }]`
- The **MCP server instructions** and the **planning guide's brief template** gain this line:

> When you stop work on a Task or Bug, send the commits you made for it: report_task's commits for a task of a plan, add_commits for anything else. Each commit is its repository, as `git remote get-url origin` prints it, and its full sha (`git log --format=%H origin/main..HEAD`, only your own commits on a shared branch). Commits go on Tasks and Bugs only, never a Story or Feature. When one comes back with open_code_comments, fix each one and answer it in answered_comments.

## Decisions (Joel, 14 Nov 2026)

| # | Question | Answer |
|---|---|---|
| D1 | Where the changes show | A Changes section on the work item page, plus a full-width Changes page at `/items/<key>/changes` |
| D2 | Narrow screens and the phone app | Under 900px the section isn't rendered; a `/changes` link shows one line and a link back; the phone app shows nothing |
| D3 | Which items take commits | **Changed:** Tasks and Bugs. A bug fixed without tasks keeps its commits. Stories and Features are refused |
| D4 | Which repositories | **Changed:** any repository the instance token can read. There's no Settings › Repositories tab |
| D5 | How a commit is named | Repository + full sha. `report_task` still takes the old short sha + url and resolves it |
| D6 | Keeping the diff | **Changed:** fetched live on view, cached for a day. No stored patch |
| D7 | All commits | One combined diff when the commits follow one another; otherwise each commit in turn |
| D8 | Where comments live | In ShipLean on the work item, anchored to sha + path + side + line. Not posted to the pull request in v1 |
| D9 | Request changes | First Proposed state and unassigned, with the open comments in the brief; a box keeps the assignee and Active instead |
| D10 | Approve | First Completed state, with the approved commits recorded in History. Open comments stay open, with a warning |
| D11 | Commits in Links | Commits move to Changes; the pull request stays a link card; old commit cards migrate |
| D12 | Viewed | **Changed:** kept in the browser only |
| D13 | Diff renderer | `@pierre/diffs` with ShipLean's colours, loaded only on the Changes page |
| D14 | How agents send commits | `report_task` gains `repo` and `answered_comments`; new `add_commits`; `get_work_item` and `get_brief` return commits and open code comments |
| D15 | Linking commits from pushes | Later, not v1 |
| D16 | Measuring it | Events `task_changes_viewed`, `code_comment_added`, `task_changes_requested` and `task_changes_approved`, counted weekly against Task page views of items with commits, and against items reopened by hand in the 4 weeks before launch |

## Later, not v1

- Post open comments to the GitHub pending review or GitLab draft notes when a pull request is open, and pull PR comments back by webhook. The research note on SHIP-1460 has the API detail.
- A push webhook that links commits whose message starts with a work item key (D15).
- Changes on a Story: all its tasks' commits in one view.
