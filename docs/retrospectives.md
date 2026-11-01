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

## Action items

An action item is a work item of type `action` ("Action item"), at the same level as a story or bug: it takes points, goes into sprints and counts in velocity. Only `improve` points can become action items. Raising one from a point links the item to the point and its theme (`retro_action`, one per point). Removed action items drop out of every retro view and count.

## Verdicts

- **Mentions:** went-badly plus not-in-plan points per sprint per theme.
- **Recurring:** a tone-bad theme mentioned in at least 3 of the last 4 sprints with no closed action item on it (any sprint, project-wide) that helped.
- **Verdict**, for a closed action item only: `before` is the average mentions per sprint up to and including the sprint it closed in, `after` the average over sprints that have ended since. `helped` when after is at most half of before, `someEffect` when it is lower, otherwise `noEffect`. `tooEarly` when no sprint has ended since. A before of 0 counts as `noEffect`.
- **Status** of a theme: `recurring`, else `beingAddressed` (an open action item), else `resolved` (a helped verdict), else `quiet`.

Events: `retroChanged` ([events.md](events.md)). Tools: [mcp.md](mcp.md).
