# Inspection (SHIP-1398)

How a team is going on **velocity, quality and cost**, and a way down from each number to the work behind it. A new top-level menu, **Inspection**, holds a dashboard and one page per measure. Every number opens the list of tasks or bugs behind it, and every task opens the detail views that already exist or are planned: the Feature timeline (SHIP-1465) and agent runs (SHIP-1463).

Mock-ups, not built. Decisions D1–D5 were settled by Joel on 15 Nov 2026 (Q1–Q5 on SHIP-1398). D6–D11 are proposed. The research behind this is attached to SHIP-1398: `warp-factory-dashboard-ideas.md` and `inspection-metrics-proposal.md`.

## Screens

| File | What it shows |
|---|---|
| `screens/inspection.html` | Every Inspection page at `/p/<project>/inspection`, picked with `?view=`. |
| `screens/inspection-data.js` | Sample history (Sprints 16–24, 123 tasks, 20 bugs), and `INS.build(opts)`: the one view model that the page and the JSON view use. It plays the part of the server-side builder. |
| `screens/shared.js` | The side menu gains **Inspection** after Retrospectives, with Overview, Lead time, Quality and Cost under it. |

States of `inspection.html`:

| Query | State |
|---|---|
| *(none)* | **Overview**: three cards (lead time, escaping bugs, tokens per task), the lead-time chart, the longest tasks and escaping bugs by sprint |
| `?wave=1` | The Overview as wave 1 ships it: lead time only |
| `?view=leadtime` | **Lead time**: the chart, and a row per sprint with the split and quarter roll-up rows |
| `?view=leadtime&cut=feature` / `&cut=who` | Mean lead time per Feature, or per agent connection and people, across the sprints |
| `?view=period&p=s23` | **One sprint**: its numbers, its tasks slowest first, and how the selected task's lead time was measured |
| `…&select=SHIP-192` | Another task in the rail (SHIP-192 has rework) |
| `…&tab=features` | The sprint's **Features** tab: how much of each Feature ended in it |
| `?view=quality` | **Quality** (wave 2): escaping bugs by severity per sprint, per 100 merged PRs, the bug list and the Found before release field |
| `?view=cost` | **Cost** (wave 3): tokens per task and per merged PR, the tasks with the most tokens, tokens by Feature |
| `?view=json` | What the `inspection_metrics` MCP tool returns for the same view |
| `?cadence=weeks` | Weeks instead of sprints, for a project with sprints turned off |
| `?coverage=low` | The commit-time backfill hasn't run, so older tasks end at Resolved and read longer |
| `?range=q\|lq\|all` | This quarter, last quarter, or every sprint (default: the last 6) |

| Screen | Light | Dark |
|---|---|---|
| Overview | ![](screens/inspection.light.png) | ![](screens/inspection.dark.png) |
| Overview, wave 1 | ![](screens/inspection-wave1.light.png) | ![](screens/inspection-wave1.dark.png) |
| Lead time | ![](screens/inspection-leadtime.light.png) | ![](screens/inspection-leadtime.dark.png) |
| Lead time by Feature | ![](screens/inspection-leadtime-feature.light.png) | ![](screens/inspection-leadtime-feature.dark.png) |
| One sprint, Tasks | ![](screens/inspection-period.light.png) | ![](screens/inspection-period.dark.png) |
| One sprint, Features | ![](screens/inspection-period-features.light.png) | ![](screens/inspection-period-features.dark.png) |
| Quality | ![](screens/inspection-quality.light.png) | ![](screens/inspection-quality.dark.png) |
| Cost | ![](screens/inspection-cost.light.png) | ![](screens/inspection-cost.dark.png) |
| By week | ![](screens/inspection-weeks.light.png) | ![](screens/inspection-weeks.dark.png) |
| Backfill not run | ![](screens/inspection-no-backfill.light.png) | ![](screens/inspection-no-backfill.dark.png) |
| JSON for agents | ![](screens/inspection-json.light.png) | ![](screens/inspection-json.dark.png) |

## How the numbers are read

**Lead time (velocity).**

- A task's lead time runs from its **start** to its **last commit**.
  - Start: the first move to an Active-category state, or `pick_up`, whichever came first. This is the rule the Feature timeline already uses.
  - End: the latest `committed_at` among its commits (D1). With no commit time, the end is the task's first move to Resolved, and the row says **Resolved** instead of **Last commit**.
- The time from the last commit to Resolved is shown in the rail as *not counted*. Agents often leave tasks unresolved, and a late bulk close would otherwise inflate the number.
- A review that asks for changes is counted, because the fix commits push the end later. A review that only approves comes after the end and is not counted.
- Lead time splits into three parts that add up to it:
  - **Blocked**: time with the Blocked tag.
  - **Rework**: time from the first resolve, or from a changes-requested review, to the last commit.
  - **Active**: what is left.
- A task closed with no start recorded (a bulk edit, say) has no lead time. It is listed under *Left out*, as the timeline shows it as a diamond.

**The headline is the mean (D4).** Each part (active, blocked, rework) is a mean too, so the parts add up to the headline. A mean moves when one task takes four days. In Sprint 23, SHIP-194 sat blocked for 3.8 days on a pricing sandbox outage, and the mean shows it. The chart draws every task as a dot, so the reader can see whether one task or many moved the bar. Dots over two days sit on the top edge in red, and the longest is labelled.

**Periods.**

- The period is a **sprint** (D3).
- A task falls in the sprint in which its lead time **ended**, not the sprint it was planned in. Tasks that moved between sprints are counted once, and the rule works the same for weeks.
- A sprint belongs to the calendar quarter its end date falls in (D7). Quarter rows sum **every** sprint in the quarter, including any that the range doesn't show.
- The running sprint is drawn dashed and labelled *so far*. The cards headline the **last finished** sprint (D6).

**Weeks.** Weeks are for a project that turns sprints off. That is a future project setting (D3), noted on its own Feature. Until it exists, `?cadence=weeks` only shows what the page becomes. A week runs Monday to Sunday.

**Escaping bugs (quality, wave 2).**

- Every Bug counts as escaping unless it is marked **Found before release** (D2). This is a new optional checkbox on the Bug form, off by default. An agent that files a bug during its own review sets it.
- A bug counts in the sprint it was **filed**.
- The cards:
  - The count per sprint.
  - A severity-weighted count. The weights are a project setting, defaulting to sev 1 = 8, 2 = 4, 3 = 2 and 4 = 1.
  - The count per 100 merged PRs. This counts PRs linked to an item in the project, by the sprint they merged in. Unlinked PRs are left out.
- **Caused by** (a link from a bug to the task that introduced it) appears in the bug list as a column with **+ Caused by**. Joel picks which bug ideas to build in wave 2 (O3).

**Cost (wave 3).**

- Cost is **tokens only**, read from each task's agent runs (SHIP-1463, D21: no money in v1).
- The mean is over tasks **with** runs. Tasks done without an agent are left out of it, not counted as zero.
- Tokens per merged PR is the sprint's total tokens divided by its merged PRs.
- Money appears once traces carry a reported cost.

**One caveat on every page:** *These numbers tell you where to look, not what caused a change.*

## Where the data comes from

| Value | Source | New? |
|---|---|---|
| Task start | State history: first move to Active, or `pick_up` | No |
| Commit time | `committed_at` on each commit sent to `report_task` (D1) | **New field** |
| Commit time, older tasks | A one-off backfill on existing installs (D1) | **New, one-off** |
| Resolved | State history: first move to a Resolved-category state | No |
| Blocked hours | The Blocked tag's history, as the timeline uses | No |
| Rework | First resolve, or a changes-requested review (`log_review`), to the last commit | No |
| Reviews, commits | `log_review` events, `report_task` commits | No |
| Found before release | A new optional field on Bug (D2) | **New field** |
| Caused by | A new link type, Bug → Task (wave 2, if picked) | **New link type** |
| Merged PRs | `link_state.merged_at`. GitHub stores it today; the GitLab merge request hook needs to store it too | **GitLab change** |
| Tokens | SHIP-1463 agent runs | After SHIP-1463 |

## What gets built

One Feature in three waves (D5).

**Wave 1: lead time.**

- `committed_at` (optional, UTC) on each commit in `report_task`, stored with the commit. The planning guide and briefs tell agents to send it (`git log -1 --format=%aI <sha>`). It uses the author date, because rebases and squashes rewrite the committer date.
- The backfill for existing installs (O1).
- The lead-time view model and its API: one request per screen.
- The Inspection menu with Overview (the lead-time card and chart, and Longest tasks) and Lead time (the range picker, the cuts, quarter rows, the period drill-down with Tasks and Features, and the rail).
- The `inspection_metrics` MCP tool. It returns the same view model, so retro and planning agents can quote the numbers.
- Analytics: Inspection viewed (per view), a period opened, a task opened from the rail. Each is compared with Retrospectives › Insights views per week over the same weeks.

**Wave 2: quality.** The Found before release field on Bug, GitLab `merged_at`, the Quality page and its Overview card, and the bug ideas Joel picks (O3).

**Wave 3: cost.** Blocked by SHIP-1463. The Cost page and its Overview card from the agent runs, with a roll-up cache for Tempo + Loki installs, where runs are found by querying on demand.

## Decisions

| | Decision | By |
|---|---|---|
| D1 | Lead time ends at the last commit. Commit times come **only** from a new `committed_at` on `report_task`, plus a one-off backfill for existing installs. They are not looked up from the code host, and push webhooks are not used. | Joel, 15 Nov |
| D2 | An escaping bug is every Bug unless it is marked **Found before release**, a new optional field. | Joel, 15 Nov |
| D3 | The period is a sprint. Turning sprints off is a future project setting; Inspection switches to weeks when it exists. The note goes on that Feature. | Joel, 15 Nov |
| D4 | The headline aggregate is the **mean** ("it sounds strange but it actually works"). | Joel, 15 Nov |
| D5 | One Feature, with bugs and cost as later waves. | Joel, 15 Nov |
| D6 | The cards headline the last **finished** sprint. The running one is shown as *so far*. | Proposed |
| D7 | Quarters are calendar quarters, and a sprint belongs to the quarter its end date falls in. Q4 asked about quarters too, but only the aggregate was answered. | Proposed |
| D8 | Rework counts from the first resolve, or the first changes-requested review, to the last commit. This includes waiting for that review. | Proposed |
| D9 | A task with no start recorded is left out and listed, never given a made-up start. This matches the timeline's D4. | Proposed |
| D10 | Desktop only in wave 1, like Task changes and Agent runs. A phone layout comes later. | Proposed |
| D11 | Escaping bugs count in the sprint they were filed, until a Caused by link exists to count them against the sprint their cause shipped in. | Proposed |

## Open questions

- **O1. How the backfill runs.** Proposed: an instance-admin MCP tool, `backfill_commit_times`, that takes `[{sha, committed_at}]`. An agent with the repos checked out fills it from `git log` for every sha already stored on the install. The alternative is a one-off SQL script in the runbook.
- **O2. A last-commit marker on the Feature timeline.** Timeline bars end at Resolved (SHIP-1465 D2), while lead time ends at the last commit. Proposed: add a tick at the last commit on each bar, so a drill-down from Inspection shows the same end. It would ship in wave 1.
- **O3. Which bug ideas go in wave 2.** The options are Caused by, escape lag, containment ratio, bug-fix lead time, bugs by Feature and reopen rate (see `inspection-metrics-proposal.md`). Caused by and containment ratio are recommended first.
