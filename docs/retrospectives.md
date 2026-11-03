# Retrospectives

Agents and people log what went well, what went badly and what can be improved. ShipLean groups the points into themes, shows them on each sprint's Retro tab and under Retrospectives (Search and Insights), and tracks whether the action items raised from them helped.

## What agents log and when

- **A sub-agent** calls `log_retrospective` on its story when it stops, done or not (`partial` when the work is unfinished). Points go in `went_well`, `went_badly` (each with a `root_cause`), `improve` and `not_in_plan` (what came up that the plan did not foresee). Its raw `failure_log` (`blockers`, `assumptions_disproved`, `friction`) is stored as logged.
- **The conductor** calls it on the Feature when the last story closes, with `compiled_from` set to the stories it compiled from, as a blameless site reliability engineer: root causes, nothing softened. A retro with `compiled_from`, or on a Feature, has the role conductor; any other is subAgent.
- A second call from the same agent name on the same item appends to that retro. `sprint_id` never changes on append.
- Both tools open with the learning-from-failure text, and the planning guide opens with it too. The goal is an accurate report, not a flattering one.
- An agent's points are never edited or archived; only their theme moves. A person's own points can be archived by any project member. Archived points drop out of every view and count.
- The Markdown run retro (`retro-<date>-<topic>.md`, budget, timeline, constraint breaks) stays; it is a separate record.
- `planning_guide` with a `project` returns lessons: the Markdown retros' "What to change next time", then the `improve` points of the 3 newest conductor retros on live Features.

## Themes

`theme_mode` in the reply says which mode the project is in.

- **auto** (only when `SHIPLEAN_RETRO_AUTO_THEMES` is `on`, which is experimental and off by default, and the embedder is semantic): ShipLean assigns each point to the nearest theme or starts a new one named from its first point (at most 6 words and 48 characters). A `theme` an agent sends is ignored and the reply warns about it.
- **pick** otherwise (the default): an agent may send a `theme` per point, reusing a name from the project's theme list (returned by `list_retrospectives` and `log_retrospective`) or naming a new one. A point with none stays Unthemed.
- People always pick a theme from the list or add one. A theme can be renamed or merged; a merge moves its points and actions to the target and recomputes the tone.

## Deciding points

Every point except Went well gets a decision, which is one of three: **undecided**, **actioned** (it has a live action item) or **Not needed** (with a reason). Went well points have no decision.

- **Not needed** needs a reason and can be undone, which clears the decision and returns the point to undecided. Undoing a point that is not Not needed is a conflict.
- A point that is already decided cannot be decided again; the second attempt is a conflict, and agents see it as `skipped`.
- People decide in the UI (the Sprint Retro tab and a Retrospectives item page); agents use `decide_retro_actions` ([mcp.md](mcp.md)).

## Action items

An action item is a work item of type `action` ("Action item"), at the same level as a story or bug: it takes points, goes into sprints and counts in velocity. New ones start in New with no sprint, so pulling one into a sprint is the human approval.

- One action item covers many points, from Can be improved, Went badly or Not in the plan. Add points to an open one with Add to existing (`existing` for agents). A point belongs to at most one live action item.
- It has a title, which a new one requires, a **type of fix** (skill, guide, test, analyzer, product or other) and one **theme**: the one given, otherwise the commonest among its points, and editable afterwards. Verdicts judge that theme. An action item raised before decisions existed has no type of fix and keeps it empty until someone sets one.
- It quotes its points and links back to them. A person creates one from `POST api/projects/{id}/retros/actions`; the old route that made one from a single point is gone.
- An action item with no tasks shows no Tasks section on its page; one with tasks lists them, with no way to add more from that page. The Taskboard can still add a task under an action item.
- Removing an action item moves it to Removed and returns all its points to undecided (open decision: Joel has not confirmed this). Removed action items drop out of every retro view and count.
- A Feature's rail lists its children's action items (open decision: not confirmed).

## Proposals

An agent can propose merging two themes (`merges` in `decide_retro_actions`). Nothing merges until a person chooses **Merge** or **Keep apart** on Insights. A pair that already has a proposal, merged or kept apart, is not proposed again. An agent also never overwrites a theme a person chose.

## Decide what to do

`decide_retro_actions` is the agent step after the conductor compiles a Feature's retro (a `planning_guide` step, never automatic). It takes one scope (a Feature, a sprint, or a date range) and previews before it applies. A Feature scope means the Feature plus its stories and bugs. It creates at most 3 new action items per call; adding to existing ones and marking Not needed are unlimited, and the rest stay undecided for the next run. Apply does what the preview's plan says, and retries on a conflict while the undecided set shrinks.

## Insights by sprint or week

The range picks sprints (`6`, `3`, `all`) or weeks (`w6`, `w12`), which drives Recurring and the verdicts as well as the columns.

- **Sprint mode** has a **No sprint** column. It counts in Total and never in Recurring or a verdict, because it has no time order.
- **Week mode** places each point in the week it was logged (Monday to Sunday, UTC) and has no No sprint column. A week counts as ended once its Sunday has passed.
- **Unthemed** comes last, marked "not judged": it has no status and no verdict. It counts every unthemed point except Went well (open decision: Joel has not confirmed this).
- The Status and Last change headings have tooltips, and each status chip explains itself.

## Verdicts

- **Mentions:** went-badly plus not-in-plan points per sprint per theme.
- **Recurring:** a tone-bad theme mentioned in at least 3 of the last 4 sprints with no closed action item on it (any sprint, project-wide) that helped. In week mode it is 3 of the last 6 weeks, fixed at 6 whatever range is shown.
- **Verdict**, for a closed action item only: `before` is the average mentions per sprint up to and including the sprint it closed in, `after` the average over sprints that have ended since. `helped` when after is at most half of before, `someEffect` when it is lower, otherwise `noEffect`. `tooEarly` when no sprint has ended since. A before of 0 counts as `noEffect`.
- **Status** of a theme: `recurring`, else `beingAddressed` (an open action item), else `resolved` (a helped verdict), else `quiet`.

Events: `retroChanged` ([events.md](events.md)). Tools: [mcp.md](mcp.md).
