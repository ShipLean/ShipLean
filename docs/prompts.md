# Prompts

ShipLean tells agents how to plan, brief, review and report through built-in Markdown prompts. SHIP-744 makes them visible and lets a project change them. This page says what is built. The design is in [`design/ship-744-prompts.md`](../design/ship-744-prompts.md).

## Where it is

**Project settings › Prompts** is the last tab. It lists the prompts by workflow stage (Intake, Shape, Plan, Build, Review, Retro, Sprint demo). Each one shows when it runs, which tool serves it, who reads it, the placeholders ShipLean fills in, the Markdown source and a rendered view.

- Project members read.
- Project admins change. A write from anyone else is refused.
- On a phone the page is readable. Editing is meant for a desktop.
- The tab's "N custom" badge appears once the tab has been opened in the session. It counts prompts that are Custom or Built-in updated, not ones that are Using built-in.

## The 11 prompts

| Stage | Prompt (key) | Served by | Read by |
|---|---|---|---|
| Intake | Issue proposal (`issueProposal`) | the issue intake round | the intake agent |
| Shape | Work from idea to done (`work`) | `planning_guide` part `work` | any agent at the start of a session |
| Plan | Planning and breakdown (`guide`) | `planning_guide` (default part) | the conductor |
| Plan | Plan reviewer (`planReviewer`) | `planning_guide` part `plan_reviewer` | a reviewer |
| Build | Lead brief (`lead`) | `get_brief` on a story | the lane lead |
| Build | Task brief (`brief`) | `get_brief` on a task | the implementer |
| Build | Report (`report`) | added to every task brief | the implementer |
| Review | Code reviewer (`codeReviewer`) | `planning_guide` part `code_reviewer` | a reviewer |
| Review | Fix round (`fix`) | `planning_guide` part `fix` | the implementer |
| Retro | Run retro (`retro`) | `planning_guide` part `retro` | the conductor |
| Sprint demo | Sprint demo video (`sprintDemo`) | `create_sprint_demo` and the demo session ShipLean starts | the demo agent |

Two things stay built-in, and the page lists them as such: the 14 inserted parts of the sprint demo (they depend on where the agent runs), and the server instructions (sent when the agent connects, before it picks a project).

## Versions

- Customize starts from the text in use. Each save needs a change note (at most 200 characters) and becomes custom v1, v2 and so on.
- The text may not be empty and may not be over 64 KB (65,536 UTF-8 bytes).
- **Use built-in** stops using the custom version. The project then reads the built-in again.
- **History** lists every version, newest first. **Use vN** puts an earlier one back in use.
- Nothing is deleted. Versions stay forever; Use built-in and Use vN only move the pointer to the one in use.

## Agent-specific sections

A line of text on its own, `<claude-code>`, `<codex-cli>` or `<cursor>`, opens a section. The matching closing tag on its own line ends it. Only that agent gets the lines between. Any other agent gets the untagged text.

- Tags sit on their own line and do not nest.
- Tags inside a code fence are text, not tags. An unclosed fence is not a fence.
- An unknown tag is kept as typed. The editor flags it as a warning and suggests the nearest known tag.
- An unclosed, nested or stray closing tag is an error. It blocks Save.
- A stored text that has such an error (a migrated Issue proposal, say) fails closed: every agent gets only the untagged text, and a region that was opened ends only at its own close tag, so nothing inside it reaches another agent. The tab flags the error until an admin fixes it.

The pinned cases are in `src/ShipLean.TestSupport/DtoFixtures/prompts/prompt-sections.json`. The server (`AgentSections`) and the editor follow them.

### How the agent is recognised

ShipLean copies what DropMCP does. It reads these signals in order and takes the first one present:

1. The `clientInfo.name` the MCP client sent when it connected. The MCP transport is stateless, so ShipLean keeps it on the OAuth authorization (one per user and client): `oauth_authorization.mcp_client_name`.
2. The `originator` header.
3. The product in the `User-Agent` header (the part before the `/`).
4. The OAuth client name.

The value is made lowercase, other characters become `-`, and it is matched by substring: OpenAI's coding CLI (`codex-cli`), `cursor`, then `claude` with `code` (`claude-code`). Anything else is `other`.

The match is self-reported. It picks sections and labels. It is never used for access. Background sessions that ShipLean starts itself (the issue proposal round, and the demo session through agent-relay) have no OAuth client, so they get the untagged text only.

## Placeholders

Two templates carry placeholders that ShipLean fills when it serves them:

- Task brief: `{{common_brief}}`, `{{task_key}}`, `{{task_title}}`, `{{task_brief}}`, `{{shared_branch}}`, `{{report_template}}`.
- Lead brief: `{{feature_key}}`, `{{story_key}}`, `{{wave}}`, `{{lane}}`, `{{tasks}}`, `{{contracts}}`, `{{shared_branch}}`.
- The Sprint demo prompt has its own `{{name}}` tokens and the seven inserted parts (intro, tools, models, level, ffmpeg, return, start).

The editor checks them. If a placeholder is missing, it warns and offers **Put back**. The version still saves: the admin may mean to drop it.

## Built-in updates

Every version records the hash of the built-in it started from (`prompt_built_in`, filled at startup by `PromptBuiltInSnapshotter`). When ShipLean ships a new built-in, the custom version stays in use and the prompt shows **Built-in updated**. The admin sees only what changed in the built-in, as a patch, and chooses:

- **Apply** saves a new version with the change merged in (source `builtInUpdate`).
- **Apply and edit** opens the merged text in the editor.
- **Keep mine** declines the change. `POST …/update/keep` records the hash, so the flag clears until the next built-in change.

The page says **Applies cleanly** when the merge needs no help. Otherwise the merged text carries conflict markers to resolve in the editor.

## Issue proposal

The Proposal instructions box under Integrations moved here. Existing text was migrated as custom v1 (source `migrated`). The built-in is the one line intake served before when a project had no text of its own. See [github-issues.md](github-issues.md).

## What the agent gets

**What the agent gets** shows the layers in order, and the exact text for each agent id (`claude-code`, `codex-cli`, `cursor`, `other`), with placeholders unfilled:

1. Server instructions.
2. The prompt in use, untagged text.
3. The agent's own sections.
4. Ways of working (SHIP-568). Planned, not built.
5. Client notes (SHIP-246). Planned, not built.

## Runs

Each time ShipLean serves a prompt, it writes a `prompt_serving` row: project, prompt, version (null for the built-in), the built-in hash, agent, the client name and which signal gave it, the item key, the person and the time. Inspection shows a run the prompts served for the run's items inside the run's time window. It is not a link from one session to one serving, so two runs of the same item at the same time share a list.

## For developers

- `IBuiltInPrompts` (`ShipLean.Contracts.Prompts`, class `BuiltInPrompts` in `src/ShipLean.WebApi/Features/Prompts`) is the only reader of the built-in files. A guard test (`BuiltInPromptReadsTests`) enforces it.
- Everything that gives an agent a prompt goes through `IPromptResolver`. Four call sites: `GuideTools` (`planning_guide`), `BriefTools` (`get_brief`), `RoundJobWorker` (the intake round) and `DemoRunStarter` (the sprint demo).
- `IConnectedAgent` (`RequestConnectedAgent`) is the one place that names the agent. Resolution off a request takes the person from `PromptRequest.UserId`.
- Routes (`ProjectPromptsController`, `api/projects/{id}/prompts`): `GET` (page), `GET {prompt}/history`, `GET {prompt}/assembled?agent=`, `POST {prompt}/versions`, `POST {prompt}/use-built-in`, `POST {prompt}/versions/{number}/use`, `POST {prompt}/update/keep`. Reading routes need a member, writing routes an admin. The tab's usage posts are `POST api/projects/{id}/usage/prompts-opened` and `…/prompt-customize-opened` in `ProjectUsageController`.
- Tables (`db/migrations/202611211200_prompts_versions.sql`): `prompt_built_in`, `prompt_version`, `project_prompt`, `prompt_serving`.
- Analytics: `prompts.opened`, `prompt.customize_opened`, `prompt.saved`, `prompt.update_applied`, `prompt.update_kept`, `prompt.built_in_used`, `prompt.version_restored`. How to read them each week is in [analytics.md](analytics.md#prompts-weekly).
