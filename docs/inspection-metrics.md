# Inspection: lead time, quality and cost

Inspection (`/p/:key/inspection`, the side menu's **Inspection** section) shows how long a project's tasks take from start to
last commit, per sprint, how many bugs escaped, and how many agent tokens a task took, so a team can see where to look. Every page carries the same caveat: *these numbers tell you where to
look, not what caused a change.* The design and its decisions are in `design/ship-1398-inspection.md`; the code is named
`InspectionMetrics` (`Features/InspectionMetrics`, `features/inspectionMetrics`) because `Inspection` belongs to agent runs.

## What it shows

| View | `?view=` | What is on it |
|---|---|---|
| Overview | `overview` (default) | The lead-time card: the last finished sprint's mean, its change against the sprint before, the running sprint *so far*, and this quarter to date against last quarter. The six slowest tasks of the range. A banner when commit coverage is low. |
| Lead time | `leadtime` | A chart with one bar per sprint (its mean) and one dot per task; dots over two days sit on the top edge. A row per sprint with the mean split into active, blocked and rework, quarter rows, an optional cut by Feature or by who did the task, and the tasks *left out*. |
| One sprint | `period` | The sprint's numbers, its tasks slowest first (or its Features), and a rail for the selected task: its segments on one track and how it was measured, event by event, with links to the task and its Feature timeline. |
| Quality | `quality` | Cards for the last finished sprint (escaping bugs, per 100 merged PRs, severity-weighted, found before release), a bar per sprint stacked by severity, a row per sprint and quarter, and the bugs counted in the range, newest first, each with its cause or a *+ Caused by* link. The Overview gains a quality card with the change against the sprint before. |
| Cost | `cost` | Cards for the last finished sprint (tokens per task with this quarter and last, tokens per merged PR, the share of tasks with agent runs), a bar per sprint, a row per sprint and quarter, the range's 8 tasks with the most tokens (each opens the task, whose Agent runs section lists the runs), and tokens by Feature. The Overview gains a tokens card with the change against the sprint before. |
| JSON | `json` | The view model as the API and the `inspection_metrics` MCP tool return it. |

The range picker shows the last 6 sprints (default), this quarter, last quarter or all sprints. Desktop only for now.

## How the numbers are read

- **Start** is the task's first move to an In-progress state (`pick_up` does this). It is the Feature timeline's start rule.
- **End** is the task's **last commit** time, whatever its state: a task still In progress with a commit time counts as ended,
  and moves to a later sprint if more commits arrive. With no commit time, the end is the first move to Resolved or Completed,
  and the task reads *Resolved* instead of *Last commit*.
- The time from the last commit to Resolved is the **unresolved tail**. The rail shows it, and the Approved review or Resolved
  move in it, as *not counted*.
- Lead time splits into parts that add up to it: **blocked** (time with the Blocked tag), **rework** (from the first resolve or
  the first changes-requested review to the end, less blocked time) and **active** (the rest).
- A commit dated before the start gives a lead time of 0 h; it is never negative.
- A task closed with no start recorded has no lead time. It is listed under **Left out** with the reason, never given a made-up
  start.
- A task counts in the sprint its lead time **ended** in, not the one it was planned in. An end in the two days after a sprint's
  last day (the weekend) still counts in that sprint.
- Every figure is a **mean**, so the parts add up to the headline, and one slow task moves it: the dots show whether one task or
  many did. The change is the whole-percent change of the mean against the previous started sprint.
- A sprint belongs to the calendar quarter its end date falls in. Quarter rows sum every sprint of the quarter, shown or not; the
  quarter to date compares with last quarter up to the same day.
- **Who** is the agent connection that started the task, else the person who started it, else its assignee.
- Hours are rounded to one decimal on the server, and every label is made on the server in the viewer's time zone.

## Quality: escaping bugs

- An **escaping bug** is every live Bug not marked **Found before release**. The flag is on bugs only: the Bug form's checkbox,
  the API, `capture_work`, `refine_work_item` (`found_before_release`), and `get_work_item` shows it. Found bugs are listed
  and counted as *found before release*, never as escaping.
- A bug counts in the sprint it was **filed** in, until it has a **Caused by** link to the Task that introduced it; then it
  counts in the sprint that Task's lead time **ended** in (the rules above, weekend grace included). A cause with no end yet
  leaves the bug in its filed sprint.
- **Caused by** joins a Bug to a Task only. Add it from the bug's Links (*+ Caused by*, or open the bug with
  `?addLink=causedBy`, which the Quality list's *+ Caused by* does), or over MCP with `link` and `type: caused_by`. The Task shows
  the bug under *Caused*. A Feature's link picker never offers it.
- **Severity-weighted** sums the project's weights over the escaping bugs: 8, 4, 2 and 1 for severity 1 to 4 by default, set by a
  project admin on Settings › General. A bug with no severity weighs 0 and shows as *Unset* in the severity split.
- **Merged PRs** counts the pull requests linked (a url link) to an item of the project that merged in the sprint, from GitHub's
  and GitLab's merged time; a PR with no link to the project is left out. **Per 100 merged PRs** is escaping bugs per 100 of
  them, to one decimal, and reads *—* when no PR merged.
- The change on the Overview's quality card is the whole-percent change in escaping bugs against the previous started sprint.
  There is none for the first sprint, for a quarter or the range, or when the sprint before had no escaping bug.
- Quarter rows and the range sum their sprints as lead time's do. With no live bug in the project at all, every quality figure
  is null and the view and card say *No bugs yet*.

## Cost: tokens per task

- **Tokens only**, never money (SHIP-1463 D21): a run's input plus output tokens, labelled `950`, `18.2k` or `1.2M`.
- Tokens come from the **agent runs** of Inspection (agent runs on work items, see [optional features](optional-features.md)).
  The Cost view and card need that feature on for the instance and the project; with it off, the view says *Agent runs are
  off* and the Overview has no tokens card.
- Only the **ClickHouse** store can total tokens per task. On a Tempo + Loki install the view and card read *—* with *Tokens per
  task needs the ClickHouse store* (item D20); Tempo support is a follow-up. An unreachable store reads *—* with the reason.
- A run counts toward the tasks at its **strongest link** only (tagged, then a ShipLean file, then a tool call), its tokens
  **split evenly** among them, so a sprint's total never counts a run twice (item D21). A run linked to the same task by a tag and by
  a tool call counts once, for the tag. A run's tokens are its spans' when any span reported them, else its logs'. A run that
  reported **no tokens** is not counted: the task then has no runs, not zero tokens.
- A task belongs to the sprint its lead time ended in (the lead-time rules, weekend grace included); the runs read are those seen
  from a day before the earliest start of the shown tasks until now.
- **Tokens per task** is the mean over the tasks **with runs** only; a task with no runs is left out, never counted as 0, and *with
  agent runs* shows the share that had any. The change on the card is the whole-percent change in that mean against the previous
  started sprint.
- **Tokens per merged PR** is the sprint's total over its merged PRs, the same count as the Quality view's; it reads *—* when no
  PR merged.
- **Kept window:** ClickHouse keeps runs for `SHIPLEAN_INSPECTION_CLICKHOUSE_RETENTION_DAYS` + 30 days (60 by default; raised on
  our production through item D23). A sprint, quarter or range that started before that reads *—*, with *not kept*: the runs are
  gone, so its tokens are unknown, not zero.

## Commit times and coverage

Commit times come from `committed_at` on each commit sent to `report_task` (`git log -1 --format=%aI <sha>`), stored on the
commit's link, and from the authored time of commits of a repository ShipLean can read. **Coverage** is the share of the range's
tasks that ended at a commit time; below 70% the Overview shows a banner and the Lead time toolbar's coverage chip takes the caution colour, because the rest
end at Resolved and read longer. On an install with commits reported before `committed_at` existed, an instance admin fills the
times once: see [Backfilling commit times](runbooks/backfill-commit-times.md).

## For agents and the API

- `GET api/projects/{projectId}/inspection-metrics?range=&cut=&period=&select=` returns `InspectionMetricsViewDto`
  (`ShipLean.Contracts/Views/InspectionMetrics`). An unknown range, cut or period is a validation error; a non-member gets
  `not_found`.
- The `inspection_metrics` MCP tool (`project`, `range`, `cut`, `period`) returns the same view in snake_case, plus `open_url`.
- The page posts `usage/inspection-metrics-viewed`, `-period-opened` and `-task-opened` (see [analytics](analytics.md)).
