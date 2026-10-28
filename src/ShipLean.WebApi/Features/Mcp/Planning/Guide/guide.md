# Planning and breaking down work for several agents

Use this when a Feature or Epic is too big for one agent, or when you will run several agents side by side. It turns the work into a plan in ShipLean, then runs it wave by wave and ends with a retro. Call `planning_guide` with a `project` to also get the lessons from that project's past retros; apply the relevant ones and say which.

When you plan an existing Feature, read its attachments with `read_attachment` and its log with `get_work_item` first, then capture the plan under that Feature's key.

## Where state lives

ShipLean holds progress for a plan run. Do not write amendments, notes for later waves, or reports to files on the person's machine.

- Brief changes before or during a wave: edit the task's description, or the Feature's Contracts section or an attachment, and leave a `comment` saying what changed and why.
- Notes for a later wave: a `comment` on that wave's story or on the Feature, tagged so `get_brief` includes it.
- Reports: `report_task` only.
- Reviews: `comment` on the task starting with `Review · `.
- Fix rounds: comments on the task.
- Facts that differ per machine (paths, SDK location, concurrency caps): say so in a `comment` so the next host re-checks them instead of trusting them.

## How a plan is stored

| In the plan | In ShipLean |
|---|---|
| The plan | A Feature |
| A lane of a wave | A Story, tagged with its wave and lane |
| An agent's slice of work | A Task under that Story |
| The slice's brief | The Task's description |
| The status table | `plan_status` |
| An agent's report | `report_task` |
| The retro | A Markdown file attached to the Feature with `attach_markdown` |
| A merge request | A pull request (PR) |

The Feature carries the shared branch (as a tag) and has the common brief (read by every agent before its own brief) and the plan document attached, all done by `capture_plan`.

## The four constraints

Every plan is designed around these. Where a later step seems to conflict with them, they win.

1. **Context window.** Agents get noticeably worse past about 100k tokens of context: they hallucinate and make mistakes. Every unit of work given to one agent (implementer, reviewer, conductor or lead) must finish inside about 120k tokens, 150k at the very most, counting its reads, the code it writes, test output and review fix rounds. Size work to the window (Step 2), not to lines or hours.
2. **No dependencies between agents running in parallel.** Agents in the same wave never need anything from each other: not a file, a merged commit, a fixture, test data or a decision. The only allowed wait is between waves: one wave finishes, the next starts. Put as much as possible into each wave.
3. **Design for flow, not for waits.** A team can execute end to end without a handoff and without waiting on anyone else. Each agent finishes its work with its brief and the contracts, and nothing else. No "wait until the other task has finished", no watching the disk for a file to appear, no polling. A parent learns that a child is done from the child's completion notification. If the breakdown needs a wait, the breakdown is wrong; fix the breakdown.
4. **Hand off only at contracts, and Agent 0 builds them first.** Work is split along contracts, and the contracts are the seams. Agent 0 builds the skeleton and every contract (interfaces, endpoints returning fake data, shared data shapes, test doubles, contract tests) before anyone else starts. Every later agent builds against a finished contract, never against another agent's work in progress.

Example: an endpoint between a page and a service. Agent 0 ships the controller returning fake data in the final shape, the data shape, and a contract test. Then, in one wave, one agent builds the page in front of the endpoint and another builds the real service behind it. Neither waits; they meet at the contract.

## Step 0: Read past lessons

Call `planning_guide` with the `project` key. Its `lessons` list holds the "What to change next time" text of each past retro, with the Feature and retro it came from. Apply anything relevant to this plan and say which lesson you applied.

## Step 1: Size the work and pick a mode

| Mode | Use when | Shape in ShipLean |
|---|---|---|
| Direct | 1 or 2 tasks, one area, the whole job fits one window of about 100k tokens | One Story. No leads, no implementer sub-agents: you do it in this session, task by task. |
| Orchestrated | 3 to 8 tasks, one repository, the plan fits in the orchestrator's head | One Story per wave. This session is the orchestrator: it writes briefs, launches sub-agents, reviews and integrates, and never writes product code itself. |
| Super agents | More than about 8 tasks, several independent feature areas or repositories, or a plan that alone would take the orchestrator past about 60k tokens | One Story per lane. This session is the conductor and launches one lead per lane; each lead runs the orchestrated loop for its lane. |

Direct mode still captures its one Story with `capture_plan` (Step 4, without the plan review) and closes it with `update_progress` (Step 7, item 7). It skips Steps 5 to 8, except that production code is reviewed by a reviewer from a different model family than the author, and a retro is written only if something went wrong.

Ask before adding phases. One shared branch, one PR, squash-merged at the end.

## Step 2: Break the work down

### Agent 0: the skeleton and the contracts

Agent 0 runs in wave 0, before anything else. It builds the thinnest path that runs end to end with real wiring, plus everything later agents hand off at:

- **Contracts** between parts of the system: interfaces, endpoints returning fake data in the final shape (including error bodies), message payloads, file formats, data shapes. Each has one named owner and a contract test: a round-trip test for data shapes, and a test the real implementation must pass, which the task building that implementation turns green.
- **Test doubles and fake data** behind each contract. If an agent would wait on another only to get test data, that is a contract Agent 0 should have shipped.
- **Hot shared files**: route tables, dependency registration, index exports, migration numbering, and shared test infrastructure (page objects, helpers, fixtures). Agent 0 creates them; later tasks only make a small append (an import plus a call), and the brief says so. Do not write "a one-line append": linters can make one line impossible.
- **Guard rails** for banned patterns: a lint rule or architecture test (for example, no test-only switches in production code), so a violation fails the build instead of a review round. Never schedule a guard rail alongside the work it guards.
- **Expensive test fixtures** (database containers, browsers, app hosts) set up so parallel agents reuse one instance with isolated data instead of starting a new one on every run. If the repository cannot do that yet, Agent 0 fixes it, or the plan lowers concurrency and records it as friction.
- **Frozen files.** Before files freeze after wave 0 (CI configuration, package manifests), list what every later task needs from them and add it now.

Temporary behaviour Agent 0 ships (fake data, stub clients, fallbacks) gets a plain TODO comment saying what replaces it, with no task key (keys mean nothing once the branch is merged). The plan lists each one with its file and the task that replaces it. Agent 0's tests assert real behaviour only; an assertion on a placeholder belongs to the task that replaces it.

Agent 0 has a context window too. If the contracts do not fit one agent, split wave 0 by area into several agents with no edges between them. Budget about 1.5 times your line estimates when Agent 0 is heavy on schemas, enums and test harnesses.

### Stories and tasks

A lane delivers a feature area end to end, named by the journey. It becomes a Story. Within a lane, tasks are cut at Agent 0's contracts. A task may be one side of a contract (the page in front of an endpoint, the service behind it) because the contract test pins both sides. For small work, one task does the whole journey.

What is never allowed is a layer cut with no contract at it: a "database tables" task, an "API endpoints" task that the UI waits for, a "wire it all up" task at the end. Those can only be tested after the others land, which builds waits into the plan and hides integration bugs until the end.

Example, building a page:

- Layer split, waits built in: tables and migration; repository and service; endpoints (waits on the service); components (wait on the endpoints); wire it all up (waits on all); tests.
- Contracts first, then one wave: Agent 0 ships the route, the page shell, the read and update endpoints returning fake data in the final data and error shapes, contract tests, fake data, and the registration entries, with one acceptance test (the page loads with one fake field). Wave 1, all in parallel, no edges: list renders rows and the empty state; filter by status; edit a row and save; the real read and update behind the contract, turning the contract tests green against the database; error and permission states against the contract's error shapes.

**The contract rule.** If one side breaks the contract, the task that owns that side fixes it, or stops and reports. The other side never compensates (hardcoding an empty list, catching a bare error, adding a fallback). Mocks return the contract's shape, not the server's, so only the contract tests prove the real side matches.

**Failing tests are allowed.** A task may start with a failing test and push it red, and Agent 0's contract tests may be red against the real implementation until its task lands. The shared branch must always build; a red test belongs to the task that will turn it green and says so in its name or a comment with the task's key; never push a change that turns another task's green test red.

### Size every task to the context window

Estimate each task's context before launch:

| Item | Tokens |
|---|---|
| Brief and common brief | measure: bytes divided by 4 |
| Required reads | measure the listed files: bytes divided by 4 |
| Code written | about 25 per line (writing it and reading it back) |
| Test runs | about 2 to 5k per run; count the expected inner-loop runs |
| Review fix rounds | about 15 to 20k each; plan for one |

Over about 100k: split. These also force a split before launch, because they predict a task that runs long or overflows: more than about 1,500 lines, more than 3 spec documents, more than one deployable plus its tests, more than 3 UI surfaces, more than 2 named user journeys, or an acceptance test that depends on work outside its lane.

Estimate in tokens only, never in time. Mark the critical path; no task on it should be budgeted above about 60k tokens. If one is, split it. The same limit applies to orchestrators, conductors and leads: they read the plan, briefs and reports, never diffs (reviewers read diffs).

Each task gets a model role in `model` and its size in `context_k`. Write the short value from the table, never a model or vendor name; it becomes a tag that `plan_status` shows.

| Value for `model` | Role | Use for |
|---|---|---|
| strong | The strongest model | Planning, the conductor, leads, Agent 0, ambiguous or design-heavy tasks |
| fast | A fast cheap model | Grunt work: boilerplate, renames, scaffolding tests from a pattern, mechanical migrations, lint fixes, lookups |
| mid | A mid-range model from another family | Well-specified medium tasks, to spread load or get an independent attempt |
| reviewer | A reviewer from a different model family than the author | Plan review and code review, so blind spots do not overlap |

A fast cheap model takes the nearest workaround unless the brief bans it by name. When one implements a design-heavy task, make the brief's banned patterns more explicit.

### Waves

The plan lists its waves. Wave 0 is Agent 0. Wave 1 is ideally everything else. A wave 2 exists only when a task needs a wave-1 result that genuinely cannot be a contract, and the plan says why. A wave label is a number such as 0, 0.1, 1 or 2; waves run in order and the stories of one wave run side by side. No task depends on another task in its own wave, within a lane or across lanes.

## Step 3: Check the plan before anyone starts

Run these checks and fix what they find:

1. **Dependency edges.** For every "B needs A", name what B needs. If it is an interface, test double, fixture, schema, test data or small gate, move it into Agent 0 (or into B itself if it is small) and delete the edge. An edge between two tasks in the same wave, including across lanes, is a defect. The target shape is Agent 0, then one wave.
2. **Contracts and shared shapes.** List every shape one task writes and another reads. Each has one owner, is defined by Agent 0, and has a contract test. Nobody writes a reader and a writer for the same shape at the same time against nothing.
3. **Placeholders and known gaps.** Look through upstream handoffs' "left undone" items and known gaps, TODOs and stubs this plan consumes, and every placeholder Agent 0 itself introduces. Each one gets an owning task, or the plan says it stays fake.
4. **File ownership.** No two concurrent tasks own the same file. List owned files per task, including test infrastructure: page objects, test helpers, fixtures and route tables.
5. **Context budget.** Every task, and every orchestrator, conductor and lead, has an estimate under about 100k (Step 2).
6. **Definition of ready.** The plan is not handed over until the briefs and the common brief exist and have passed the plan review (any edit after the review is re-reviewed), and every command in every brief's test block has been run in a throwaway worktree on this host, twice from clean, and timed. Only commands that worked go into the briefs. If an inner-loop run is over about a minute, fix the fixture setup or the filter first.
7. **Open questions.** Anything whose answer changes the work (where data comes from, which environment, who approves) goes to the user now, batched in one set of questions. Do not build something you might throw away to find out.
8. **Concurrency.** Size each wave's parallel sub-agents to the machine's test load (containers, browsers, cores), not a fixed agent count.

## Step 4: Review the plan, then capture it

Write the plan, then put it in the backlog with `capture_plan`. The plan review sits between the preview and the apply, because `capture_plan` refuses a Feature that already has plan stories: there is no second capture.

- `feature`: an existing Feature by `key`, or a new one with a `title`. Put what the Feature is for in `goal`, in a sentence or two, at most 1,000 characters. That is the Feature's Goal field; never write a Goal heading in the description. Put the contracts in `description`, in a section headed exactly "## Contracts".
- `shared_branch`: the one branch every agent pushes to.
- `common_brief`: what every agent reads before its own brief: the repository, environment, test tiers, banned patterns, git rules, the contract rule and the report rule.
- `plan_markdown`: the whole plan document, attached to the Feature.
- `stories`: one per lane of a wave, each with a `title`, `wave` and `lane` (leave `lane` out when the wave has one lane), and its `tasks`. A task has a `title`, a `brief` (goal, exact reads, do-not-read, owned files, the contracts it builds against, named acceptance tests, tests to run), a `model` value (Step 2), and `context_k`.

A plan has at most 60 items (stories plus tasks, plus the Feature when it is new). A task brief is at most 10 KB, the common brief at most 64 KB and `plan_markdown` at most 200 KB. `context_k` over 150 is an error and over 100 a warning. A wave without lanes holds one Story, and lane names are unique within a wave. An error in the preview names the field; fix it and preview again.

1. Call `capture_plan` without `plan`. That is a preview: it shows the stories, the item count and any warnings about the plan's shape, and it returns a `plan` token that expires after `expires_in_minutes`. The token is the `plan` value in the result's JSON: copy it verbatim.
2. Have a reviewer from a different model family than the author review the plan, using the `plan_reviewer` part. Give it the plan document, the common brief, the task briefs and the preview. Apply its findings, note which you rejected and why, and preview again after any change.
3. Apply by calling `capture_plan` with exactly the same arguments plus that `plan`. Changed arguments are refused, so change nothing between the last preview and the apply.

Do not launch anything before the review. After the apply, change a task's wording with `refine_work_item`; a change to the plan's structure needs a new Feature.

## Step 5: Launch the right agents

The conductor reads the Feature with `get_brief` (the Feature's key) for its waves and the first startable one, and `plan_status` for the whole picture. A wave can start when every Story of the wave before it is Completed or Removed, so every Story must be closed (Step 7, item 7).

Keep the Feature's one-line status current with `set_status`: when a wave starts, when it closes, and when the run ends.

## Step 6: Run the tasks

### Shared branch, separate worktrees

All agents push to the one shared branch, so conflicts show up early and small. Each sub-agent still gets its own worktree so working copies and test runs do not collide. Push the shared branch first, then for each task create a worktree on a branch of its own from the shared branch.

Inside the worktree the sub-agent pulls and pushes the shared branch directly: pull with rebase from the shared branch, then push HEAD to it, and on rejection pull with rebase again. If the rebase conflicts only in files the task owns, or in a small append to a shared file (keep both sides), resolve it and continue. Any other conflict: abort the rebase and report blocked with the files. A conflict outside owned files means the plan's file ownership was wrong; fix the plan, not just the conflict.

Never force-push and never amend. Commit small and often with the task's key as the message prefix, so commits can be found by searching the log. Push after each green step. Keep the worktree until the review loop approves the task, then remove it.

### Briefs

An implementer reads its brief with `get_brief` (the task's key). The brief is assembled from the common brief, the task's own description, the shared branch and the report template; see the `brief` part. A brief always contains:

- The goal, exact reads, do-not-read, owned files, and the contracts the task builds against (which it must not change).
- The definition of done as named acceptance tests, one per user journey, so the reviewer can tick each one off.
- The context budget from Step 2, and the rule: if you pass about 100k tokens before you are done, push what is green, report what is left, and stop. The orchestrator briefs a fresh agent for the rest.
- Banned patterns by name, each with the allowed alternative.
- The contract rule: if the other side of a contract is wrong, fix your side or stop and report. Never compensate.
- Test tiers: the inner loop uses the filtered command for the area changed, with a timeout so a full-suite run used as an inner loop dies by itself; each affected test project runs in full once before reporting; for acceptance tests, run each test the task added or changed individually and leave the rest to CI.
- The self-checks to run before reporting (Step 7, check 1).
- The line: if you cannot meet a constraint in this brief, stop and report. Do not work around it.

The implementer starts with `pick_up` on its task, which assigns it and starts it.

### Waiting

After launching, end your turn. A child's completion notification wakes you. That applies to orchestrators, leads and the conductor alike.

- No watchers, sleep loops, heartbeat files or polling of the disk, processes or transcripts. The breakdown is designed so nothing needs to be watched.
- Never interrupt a lead while its children are running.
- When the user asks for status, answer from `plan_status` at that moment, not from memory.
- If a completion notification never arrives, record it in the retro as a harness problem. Do not build a watcher to cover for it.

## Step 7: Review loop for every task

1. **Mechanical handoff checks first**, before any reviewer: the task's commits sit on top of the shared branch; a diff of configuration files (compiler, linter, test runner, CI, project files) against the shared branch, so a quietly widened exemption or a dropped include is caught; every red test traced to the task that owns it. The brief makes the implementer run the same checks before reporting.
2. **The report.** The implementer hands in its report with `report_task`, using the `report` part for the fields: status (done, blocked or partial), commits, tests run, acceptance tests, config files changed, red tests, context used, public surface, decisions, gotchas, what is left undone, temporary behaviour, and any constraint it could not meet. The report lands as a comment on the task, its commits and PR become links, and each left-undone item becomes an orphan task for the plan to own. Send it once per stop, done or not.
3. **The review.** Launch a fresh reviewer from a different model family than the author, in the background, with the task's brief, the commits (listed one by one, because other tasks' commits are interleaved), the task's worktree to run tests in, and the `code_reviewer` part. It returns blocking and non-blocking findings. The reviewer records its findings with `comment` on the task, starting with "Review · " so `plan_status` can count review rounds.
4. **Fix.** If there are blocking findings, resume the same implementer (same model, same context) with the `fix` part and the findings. It pushes new commits and reports the new ones with `report_task`.
5. **Re-review** only the new commits against the blocking findings. A small follow-up diff the orchestrator checks itself; a large one goes to a fresh reviewer.
6. After 2 rounds with blockers left, stop. Either re-brief a fresh sub-agent with a smaller task or bring the disagreement to the user.
7. **Close the Story.** Whoever runs a Story's review loop closes that Story with `update_progress` (`state` set to the Story's Completed state, by name, such as Closed, plus a `note` naming the commits) once every task in it has passed review: the orchestrator for its wave's Story, the conductor for Agent 0's Story, a direct-mode agent for its one Story, a lead for its lane. Before closing, give each orphan task an owner in a later wave, or leave it open and unowned and say so in the closing note. That is what lets the next wave start.

Reviews do not hold up the next wave: start it alongside a review whenever the review's fixes would be additive commits.

When a report lists left-undone items, they become orphan tasks; give each an owner or leave it visibly unowned. A task whose own definition of done appears in its left-undone list is not done.

## Step 8: Leads (super agents mode only)

- A lane is one feature area or journey, delivered end to end, with its own files. Lanes share only Agent 0's contracts. There are no gates between lanes during a wave: if a lane needs something from another lane, it moves into Agent 0 or into the lane that needs it.
- The conductor runs Steps 1 to 5 for the whole plan, ships Agent 0 through one sub-agent and the review loop, then launches one lead per lane, in the background, on the strongest model, with the `lead` part as its brief. `get_brief` on the Story's key returns the same lead brief with the lane's tasks and the contracts filled in.
- A lead runs Steps 6 and 7 for its lane. It never reads or writes product code or diffs; it reads briefs, reports and `plan_status`, and stays inside its own context budget.
- Lanes talk through ShipLean: the Stories and Tasks, the reports on them and `plan_status`. Only the conductor changes a contract; a lane that needs a contract change stops and asks the conductor.
- A lead closes its Story with `update_progress` once every one of its tasks has passed review (Step 7, item 7). That is what lets the next wave start.
- If a lead cannot launch sub-agents in its environment, it stops and returns its briefs; the conductor gives the user the launch prompts to paste into separate agent sessions.

## Step 9: Finish and retro

1. When every task is done, run the full test suite once on the shared branch in CI. Fix what it finds through the same review loop.
2. Check the plan's list of temporary behaviour: each item is gone, or still listed with an owner. Search the PR diff for TODO; every one in it matches a listed item, or the work is not done.
3. Open one PR from the shared branch, to be squash-merged. **PUT THE KEY OF EVERY STORY IT DELIVERS IN THE PR TITLE** (for example "SHIP-12 SHIP-13 SHIP-14: WHAT IT DOES"; Features and Epics are not linked). ShipLean links the PR to each Story key in the title and closes the Story when the PR merges; without the keys the Stories stay open after the merge.
4. Read `plan_status` for the timeline, then fill in the `retro` part : TL;DR, a timeline per task with wave, model and review rounds, every time the four constraints were broken (an agent waited, a wave had an edge, an agent passed about 100k, a handoff happened outside a contract), what caused delays (measured from the timeline), what to change, and the token budget the run would have needed with the changes. Do not estimate time.
5. Attach the retro to the Feature with `attach_markdown`: `feature` is the Feature's key, `name` is `retro-<yyyy-mm-dd>-<topic>.md` with a real date and a short topic, and `content` is the Markdown. Attaching the same name again replaces the file. That file is where `planning_guide` reads lessons from: the text under "What to change next time" is what the next plan sees, so write it as rules a future plan can apply.
6. Set the Feature's final status with `set_status`, passing `state` as the Feature's Completed state by name, so it is one call.
7. Tell the user the outcome, then "Lessons learned" as 3 to 6 bullets, then where the retro is attached.
