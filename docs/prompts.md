# Prompts

ShipLean tells agents how to plan, brief, review and report through built-in Markdown prompts. SHIP-744 makes them visible and lets a project change them. This page says what is built. The design is in [`design/ship-744-prompts.md`](../design/ship-744-prompts.md).

## Where it is

**Ways of working** is a side-menu item under Inspection, at `/p/:key/ways-of-working?prompt=<key>`; the old `/p/:key/settings/prompts` address redirects there. The prompts follow the workflow's stages (Intake, Shape, Plan, Build, Review, Retro, Sprint demo). One prompt is open at a time, full width. Each one shows when it runs, which tool serves it, who reads it, the placeholders ShipLean fills in, the Markdown source and a rendered view.

To find a prompt:

- **Stage strip.** Click a stage to open its first prompt. Each stage shows how many prompts it has and, in purple, how many are custom.
- **Chips.** Under the strip, a stage with two or more prompts has a chip per prompt; click one to open it. A dot marks a custom prompt (purple) or a built-in update (amber). A stage with one prompt shows no chips, only when it happens.
- **Search.** The box at the right of the header finds a prompt by its name, file, text in use or a placeholder. Enter opens the first match.

Moving to another prompt with unsaved edits in the editor asks first.

- Project members read.
- Project admins change. A write from anyone else is refused.
- On a phone the page is readable. Editing is meant for a desktop.
- The purple "N custom" pill in the page header is hidden at 0. It counts prompts that are Custom or Built-in updated, not ones that are Using built-in.

## The 11 prompts

| Stage | Prompt (key) | Served by | Read by |
|---|---|---|---|
| Intake | Issue proposal (`issueProposal`) | the issue intake round | the intake agent |
| Shape | Work from idea to done (`work`) | `planning_guide` part `work` | any agent at the start of a session |
| Plan | Planning and breakdown (`guide`) | `planning_guide` (default part) | the conductor |
| Plan | Plan reviewer (`planReviewer`) | `planning_guide` part `plan_reviewer` | a reviewer |
| Build | Lead brief (`lead`) | `get_brief` on a story, or `planning_guide` part `lead` | the lane lead |
| Build | Task brief (`brief`) | `get_brief` on a task, or `planning_guide` part `brief` | the implementer |
| Build | Report (`report`) | added to every task brief, or `planning_guide` part `report` | the implementer |
| Review | Code reviewer (`codeReviewer`) | `planning_guide` part `code_reviewer` | a reviewer |
| Review | Fix round (`fix`) | `planning_guide` part `fix` | the implementer |
| Retro | Run retro (`retro`) | `planning_guide` part `retro` | the conductor |
| Sprint demo | Sprint demo video (`sprintDemo`) | `create_sprint_demo` (the caller gets its own sections) and the demo session ShipLean starts (untagged text only) | the demo agent |

Two things stay built-in, and the page lists them as such: the 14 inserted parts of the sprint demo (they depend on where the agent runs), and the server instructions (sent when the agent connects, before it picks a project).

## Versions

- Customize starts from the text in use. When a project is on Using built-in with a kept custom version, the button reads "Edit custom version" and starts from that custom text. Each save needs a change note (at most 200 characters) and becomes custom v1, v2 and so on.
- The text may not be empty and may not be over 64 KB (65,536 UTF-8 bytes).
- **Use built-in** stops using the custom version. The project then reads the built-in again.
- **History** lists every version, newest first. **Use vN** puts an earlier one back in use.
- Nothing is deleted. Versions stay forever; Use built-in and Use vN only move the pointer to the one in use.

## Over MCP

An agent working for a project can read the prompts and, for an admin, change them. The tools are `prompts`, `save_prompt` and `use_prompt_version`. The full arguments are in [mcp.md](mcp.md).

- **Who.** Any project member can read with `prompts`. Only a project admin can call `save_prompt` or `use_prompt_version`. Anyone else is refused before anything is read or written.
- **Read first.** `prompts` lists every prompt, or returns one prompt with its versions and the text of the version in use (or of the version asked for). It shows which version is in use.
- **`based_on`.** A save names the version the agent read as in use: a number, or `built-in`. The agent sends the full new text and a change note, not a diff.
- **Preview, then apply.** The first call writes nothing. It returns the next version number, a patch against the text in use, the size and any warnings, with a plan. The second call repeats the same arguments and adds the plan, and then it saves. The apply call must resend the same `prompt`, `text`, `note` and `based_on` as the preview. If any of them changed, the save is refused and the agent previews again. A change only in line endings (CRLF or LF) does not count as a change, because the save normalises line endings the same way.
- **Same checks as the browser.** The note is required and at most 200 characters. The text may not be empty or over 64 KB, and a broken agent section is an error. The checks run in that order, then `based_on`, then "Nothing changed".
- **Warnings.** A missing placeholder, an unknown placeholder and a tag that is close to a known one (such as `<cludecode>`) are warnings. They say so, and the version still saves, as it does in the editor.
- **Stale refusal.** If the version in use is no longer the one in `based_on`, the save is refused with the version now in use. The agent reads the prompt again and saves again. The check runs at the preview and again under the lock when the save is applied, so a save that races another is refused too.
- **Use a version.** `use_prompt_version` puts a saved version, or the built-in, in use. Nothing is deleted.
- **History.** A save from an agent is a version like any other, with source MCP and the agent's name. History shows both.
- **Stays in the browser.** An agent cannot see or answer **Built-in updated**. Apply, Apply and edit and Keep mine are done on the page. Nor can it delete a version.

## Agent-specific sections

A line of text on its own, `<claude-code>`, `<codex-cli>` or `<cursor>`, opens a section. The matching closing tag on its own line ends it. Only that agent gets the lines between. Any other agent gets the untagged text.

- Tags sit on their own line and do not nest.
- Tags inside a code fence are text, not tags. An unclosed fence is not a fence.
- An unknown tag is kept as typed. When it is close to a known tag (within 3 edits, such as `<cludecode>`), the editor warns and offers "Did you mean `<claude-code>`". Any other unknown tag (such as `<summary>`) is plain text and passes silently.
- An unclosed, nested or stray closing tag is an error. It blocks Save.
- A stored text that has such an error (a migrated Issue proposal, say) fails closed: every agent gets only the untagged text, and a region that was opened ends only at its own close tag, so nothing inside it reaches another agent. Ways of working flags the error until an admin fixes it.

The pinned cases are in `src/ShipLean.TestSupport/DtoFixtures/prompts/prompt-sections.json`. The server (`AgentSections`) and the editor follow them.

### How the agent is recognised

ShipLean copies what DropMCP does. It reads these signals in order and takes the first one present:

1. The `clientInfo.name` the MCP client sent when it connected. The MCP transport is stateless, so ShipLean keeps it on the OAuth authorization (one per user and client): `oauth_authorization.mcp_client_name`.
2. The `originator` header.
3. The product in the `User-Agent` header (the part before the `/`).
4. The OAuth client name.

The value is made lowercase, other characters become `-`, and it is matched by substring: OpenAI's coding CLI (`codex-cli`), `cursor`, then `claude` with `code` (`claude-code`). Anything else is `other`.

The match is self-reported. It picks sections and labels. It is never used for access. Background sessions that ShipLean starts itself (the issue proposal round, and every demo session ShipLean starts) have no OAuth client, so they get the untagged text only.

## Placeholders

Three prompts carry placeholders that ShipLean fills when it serves them:

- Task brief: `{{common_brief}}`, `{{task_key}}`, `{{task_title}}`, `{{task_brief}}`, `{{shared_branch}}`, `{{report_template}}`.
- Lead brief: `{{feature_key}}`, `{{story_key}}`, `{{wave}}`, `{{lane}}`, `{{tasks}}`, `{{contracts}}`, `{{shared_branch}}`.
- The Sprint demo prompt has its own `{{name}}` tokens and the seven inserted parts (intro, tools, models, level, ffmpeg, return, start).

The editor checks them. If a placeholder is missing, it warns and offers **Put back**. The version still saves: the admin may mean to drop it.

## Built-in updates

Every version records the hash of the built-in it started from (`prompt_built_in`, filled at startup by `PromptBuiltInSnapshotter`). When ShipLean ships a new built-in, the custom version stays in use and the prompt shows **Built-in updated**. The admin sees only what changed in the built-in, as a patch, and chooses:

- **Apply** saves a new version with the change merged in (source `builtInUpdate`). It is offered only when the change applies cleanly.
- **Apply and edit** opens the merged text in the editor. When the merge has conflicts, this is the only way to take the change.
- **Keep mine** (the button reads "Keep mine as it is") declines the change. `POST …/update/keep` records the hash, so the flag clears until the next built-in change.

The page says **Applies cleanly** when the merge needs no help. Otherwise the merged text carries conflict markers (`<<<<<<< yours`, `=======`, `>>>>>>> built-in`) to resolve in the editor.

## Issue proposal

The Proposal instructions box under Integrations moved here. Existing text was migrated as custom v1 (source `migrated`). The built-in is the one line intake served before when a project had no text of its own. See [github-issues.md](github-issues.md).

## What the agent gets

**What the agent gets** shows the layers in order, and the exact text for each agent id (`claude-code`, `codex-cli`, `cursor`, `other`), with placeholders unfilled:

1. Server instructions.
2. The prompt in use, untagged text.
3. The agent's own sections. There is no such layer for `other`.
4. Ways of working (SHIP-568). Planned, not built. Shown only for Work, Planning and breakdown, Task brief and Lead brief.
5. Client notes (SHIP-246). Planned, not built.

## Runs

Each time ShipLean serves a prompt, it writes a `prompt_serving` row: project, prompt, version (null for the built-in), the built-in hash, agent, the client name and which signal gave it, the item key, the person and the time. Inspection shows, on a run's header, the prompts served to the run's items to the same agent as the run, from 5 minutes before the run started to its end (newest 20). It is not a link from one session to one serving, so two runs of the same item by the same agent at the same time share a list.

## For developers

- `IBuiltInPrompts` (`ShipLean.Contracts.Prompts`, class `BuiltInPrompts` in `src/ShipLean.WebApi/Features/Prompts`) is the only reader of the built-in files. A guard test (`BuiltInPromptReadsTests`) enforces it.
- Everything that gives an agent a prompt goes through `IPromptResolver`. Four call sites: `GuideTools` (`planning_guide`), `BriefTools` (`get_brief`), `RoundJobWorker` (the intake round) and `DemoRunStarter` (the sprint demo).
- `IConnectedAgent` (`RequestConnectedAgent`) is the one place that names the agent. Resolution off a request takes the person from `PromptRequest.UserId`.
- Routes (`ProjectPromptsController`, `api/projects/{id}/prompts`): `GET` (page), `GET {prompt}/history`, `GET {prompt}/assembled?agent=`, `POST {prompt}/versions`, `POST {prompt}/use-built-in`, `POST {prompt}/versions/{number}/use`, `POST {prompt}/update/keep`. Reading routes need a member, writing routes an admin. The page's usage posts are `POST api/projects/{id}/usage/prompts-opened` and `…/prompt-customize-opened` in `ProjectUsageController`.
- Tables (`db/migrations/202611211200_prompts_versions.sql`): `prompt_built_in`, `prompt_version`, `project_prompt`, `prompt_serving`.
- Analytics: `prompts.opened`, `prompt.customize_opened`, `prompt.saved`, `prompt.update_applied`, `prompt.update_kept`, `prompt.built_in_used`, `prompt.version_restored`. How to read them each week is in [analytics.md](analytics.md#prompts-weekly).
