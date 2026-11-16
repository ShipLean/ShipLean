# Inspection: lead time

Inspection (`/p/:key/inspection`, the side menu's **Inspection** section) shows how long a project's tasks take from start to
last commit, per sprint, so a team can see where to look. Every page carries the same caveat: *these numbers tell you where to
look, not what caused a change.* The design and its decisions are in `design/ship-1398-inspection.md`; the code is named
`InspectionMetrics` (`Features/InspectionMetrics`, `features/inspectionMetrics`) because `Inspection` belongs to agent runs.
Quality (escaping bugs) and cost (tokens) are later waves of the same page.

## What it shows

| View | `?view=` | What is on it |
|---|---|---|
| Overview | `overview` (default) | The lead-time card: the last finished sprint's mean, its change against the sprint before, the running sprint *so far*, and this quarter to date against last quarter. The six slowest tasks of the range. A banner when commit coverage is low. |
| Lead time | `leadtime` | A chart with one bar per sprint (its mean) and one dot per task; dots over two days sit on the top edge. A row per sprint with the mean split into active, blocked and rework, quarter rows, an optional cut by Feature or by who did the task, and the tasks *left out*. |
| One sprint | `period` | The sprint's numbers, its tasks slowest first (or its Features), and a rail for the selected task: its segments on one track and how it was measured, event by event, with links to the task and its Feature timeline. |
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
