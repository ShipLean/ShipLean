# Work in ShipLean from the first idea to done

ShipLean is the record from the moment something might become work until it is done. Do not keep that state in local Markdown files on your machine; put it on the Feature in ShipLean so the next person or the next session can continue.

## Stages

| Stage | In ShipLean | Tools |
|---|---|---|
| **Idea** | A Feature in the first Proposed state (New on the default workflow), with a goal. Search first so an existing Feature is reused | `search`, `create_feature` |
| **Research** | Each finding (an analysis, a comparison, notes from a source) is a Markdown attachment on the Feature. The body holds the key facts and decisions. Every session ends with a log comment and a status line | `attach_markdown`, `attach_file` (charts), `link` (sources, PRs), `comment`, `set_status` |
| **Evaluate** | A decision on the Feature: go on (move to In Progress), park it (status says why and what would revive it), or remove it. The reasoning goes in the body | `set_status` with state, `refine_work_item` |
| **Plan and break down** | The same Feature, planned by its key. Its research attachments are the input; do not create another Feature | `planning_guide`, `plan_feature`, `capture_plan` |
| **Build** | Briefs, amendments, notes for later waves, reports and reviews live on the plan's items | `get_brief`, `report_task`, `comment`, `plan_status` |
| **Done** | Retro attached to the Feature, Feature completed | `attach_markdown`, `set_status` |

States are named by category (Proposed, In Progress, Completed, Removed), so this works with the default workflow (New / Active / Resolved / Closed / Removed) and with any project's own workflow. Until Idea / Blocked / Parked / Done states exist, a parked idea stays Proposed with a status line starting `Parked:`.

## The Feature body

Keep four blocks in the description (plain Markdown):

1. **Goal** — use the Goal field, not a heading in the description.
2. **Key facts and decisions** — what is known and what was decided.
3. **Next** — the very next step for whoever picks this up.
4. **Open questions** — what still needs a person or more research.

Update the body as you learn; attachments hold the long form.

## Attachments

Name research files `<topic>-<yyyy-mm-dd>.md`. A new round of the same document replaces it under the same name; the body and log keep history attachments do not.

Read what is already there with `get_work_item` (attachments list) and `read_attachment` before you add more.

## The log

On a Feature, `comment` is its session log. End every working session with one comment that says:

- what you did this session,
- what you found,
- what is next.

Then call `set_status` even when nothing changed, so the Feature shows it was checked.

## Evaluate

Before planning, decide:

- **Go** — move to In Progress and call `plan_feature` on this Feature's key.
- **Park** — stay Proposed (or equivalent); status line starts with `Parked:` and says why and what would revive it.
- **Remove** — move to Removed / Closed and say why in the body.

## Hand over to planning

Plan the same Feature by key. Read its attachments with `read_attachment` and its log with `get_work_item` (page comments with `comments_before` when there are more than five). Then call `planning_guide` and `capture_plan` under that key.
