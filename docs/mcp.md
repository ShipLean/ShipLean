# The `/mcp` endpoint

How an AI agent (Claude Code, Cursor, Claude Desktop) talks to ShipLean. This is the door the tools sit behind. Code: `src/ShipLean.WebApi/Features/Mcp/{Transport,Scopes,Errors}`. Connecting an agent, consent, revoking and the token rules are in [`agents.md`](agents.md).

## Connecting

```sh
claude mcp add --transport http shiplean <base_url>/mcp
```

The agent calls `/mcp`, gets a 401 with a `resource_metadata` link, and the person is bounced to ShipLean's consent page in the browser (the OAuth flow served by the agents feature). The agent keeps the token it is given. `<base_url>` is `instance_settings.base_url`. Changing it changes the token audience, so every agent has to reconnect.

## Who gets in

A valid agent **bearer token** (`Authorization: Bearer ...`). The cookie is never consulted on `/mcp`, and a bearer is never accepted on `/api/*` or `/hubs`: the `agent` authorization policy names its own scheme, and everything else keeps the cookie scheme.

On every request that carries a bearer, the token goes through `IAgentTokens.ValidateAsync(bearer, resource)`, with `resource` = `<base_url>/mcp` (or the request's own origin when `base_url` is blank), both canonicalised by `AgentUrls` (lowercase host, default port dropped, path dropped) so a token issued for `https://ShipLean.Example:443` is valid at `https://shiplean.example`. There is no cache: a revoked token, a disabled user, a changed security stamp or a different audience is a 401 on the next call. The bearer is never logged. A bearer that does not validate is `invalid_token`, unless the request carries a usable gateway email header (below), which is checked first.

A valid token becomes a principal for the token's user: `ClaimTypes.NameIdentifier` is the user id exactly as for a cookie user, so `ICurrentUser` and `IProjectAccess` work unchanged. The granted scopes ride as `shiplean:scope` claims. The request's `ICurrentAgent` is the client as consent showed it (`AgentIdentity`); everywhere else it is none. Stores stamp `AgentStamp.Of(agent)` on revisions and comments.

When `SHIPLEAN_MCP_GATEWAY_EMAIL_HEADER` is set and the request carries that header, one non-blank value of it is the caller and any bearer is ignored: a gateway that signs people in with its own identity provider forwards that provider's token, which is not a ShipLean one. ShipLean finds or creates the `app_user` (a new name is the part of the email before `@`; a later call passes no name, so a name the person has changed stays). The principal has read, write and plan, and no agent, so a comment or edit is the person's own. Project membership is unchanged: someone who is not on the project gets `not_found`. A blank header, a header sent more than once, or a value that is not an email is the same as no header: the bearer is checked as usual, and with no bearer it is a 401 with no `invalid_token`. The header value is never logged. Unset, the header is ignored.

The variable does not make the header trustworthy. Set it only when a sidecar, policy proxy or ingress writes the header and removes any copy the client sent, and no client can reach `/mcp` with a header it wrote itself. Startup logs the header name when the variable is set.

## What a refused request gets

All bodies are the usual `ApiError` JSON (`{ "error", "code" }`, plus `"field"` when a request field is at fault), not JSON-RPC.

| Status | When | Headers |
|---|---|---|
| 401 | No bearer and no usable gateway email header, or a bearer that does not validate | `WWW-Authenticate: Bearer realm="shiplean", resource_metadata="<issuer>/.well-known/oauth-protected-resource/mcp", scope="shiplean:read shiplean:write"`, plus `error="invalid_token"` when a bearer was sent |
| 403 `forbidden` | `Origin` is not the server's own | none |
| 403 `forbidden` | The tool's scope is not granted (the step-up) | `WWW-Authenticate: Bearer error="insufficient_scope", scope="<granted + required, sorted>", resource_metadata="..."` |
| 405 | GET, HEAD, PUT, PATCH or DELETE (the transport is stateless: no stream, no session) | `Allow: POST` |
| 400 | Any `POST` body that is not one valid JSON-RPC message, read the way the SDK reads it: a JSON array (the spec dropped batching), malformed JSON, a `tools/call` with no usable `name` | none |
| 413 | Body over the cap: one attachment at the file limit, base64-encoded, plus 64 KB | none |
| 429 `rate_limited` | A limit below | `Retry-After` on the lookup limit |

The 405 comes from `McpMethodsController`, because the SDK maps only `POST` while `Stateless = true`. If `Stateless` is ever set false the SDK maps `GET` and `DELETE /mcp` itself and that route becomes ambiguous: remove the controller then.

A challenge is never a redirect to `/login`. `GET /.well-known/oauth-protected-resource` and `.../mcp` are anonymous (the SDK's `AddMcp` handler): `resource` = `<base_url>/mcp`, `authorization_servers` = `[<base_url>]`, `scopes_supported` = read, write, plan.

## Origin

A request with an `Origin` header is refused (403, before the token is even looked up) unless its scheme, host and port equal `<base_url>`. `Origin: null` is foreign. No `Origin` (a non-browser agent) passes. This stops a web page from driving a signed-in user's agent through DNS rebinding. **It needs `base_url` set.** With a blank `base_url` the server only knows its own name from the `Host` header, which a rebinding page controls, so a browser `Origin` cannot be verified: every request that carries an `Origin` is refused (403). Set the base URL to use browser-based clients. Agents that are not browsers send no `Origin` and are unaffected.

## Scopes

| Scope | Meaning |
|---|---|
| `shiplean:read` | Always granted. Reads, and every preview. |
| `shiplean:write` | Capture and update work. |
| `shiplean:plan` | Sprint planning applied for real. |

Every tool declares `[McpScope(scope)]` on its method or its `[McpServerToolType]` class (the method wins). A tool that previews before it applies also sets `PreviewScope = McpScopes.Read`: a call without a non-null `plan` argument then needs only that. `McpPipeline` (after authentication, before the SDK) buffers the body, parses it with the SDK's own reader and types (`JsonRpcMessage`, then `CallToolRequestParams` with `McpJsonUtilities.DefaultOptions`, so a BOM and property-name case bind exactly as the SDK binds them), finds the tool by name in the registered tools, and answers the 403 step-up **before the tool runs** when the token lacks the scope. A body it cannot read is a 400, never passed through. An unknown tool name passes through and the SDK answers it. A registered tool with no `[McpScope]` fails closed (500, the tool does not run; a guard-rail test fails on it earlier).

The call-tool filter (`ToolScopeFilter`) is the backstop: it re-checks the scope against the tool the SDK actually matched and the arguments the SDK actually bound, and that the gate counted a writing call against the write limit. If the two ever disagree the call is not run and comes back as a `forbidden` `McpError`, logged.

## Limits

- **Failed token lookups: 20 a minute per client network** (`AgentFailureLimiter`, fixed window, in memory; an IPv6 client is counted by its /64). The token is validated first: a valid token always gets in, and only a request whose token is invalid gets 429 once its network is over the limit, so someone behind a shared address cannot lock real agents out. The count is atomic and expired windows are dropped by a timer. It needs the real client address: behind a reverse proxy, list the proxy in `SHIPLEAN_TRUSTED_PROXIES` or every client shares the proxy's address.
- **Writing tool calls: 120 a minute per user** (the `agent` rate-limit policy). A writing call is one whose required scope is not read, so a preview does not count. Reads are not limited.
- **Body: one max attachment, base64-encoded, plus 64 KB** (`McpBodyLimit`). The default file limit is 25 MB, so the body cap is about 33.4 MB. The gate buffers that whole body. Kestrel's default of 30 MB is raised for `/mcp` only, up to the cap.

The client address is the connection's, after forwarded headers (`docs/auth.md`, "Behind a proxy").

## Tool errors

A tool throws `ApiException` and never catches it. The call-tool filter turns it into a result with `isError: true`, `structuredContent` = `McpError` (`{ "ok": false, "error": { "code", "message", "field", "candidates" } }`, `field` and `candidates` from `ApiException.Field` and `.Candidates`) and the message as the one line of text. Any other exception becomes `internal_error` with the fixed message and is logged; the detail never reaches the agent. A missing scope is not a tool error: it is the HTTP 403 above.

## Search

`search(words?, project?, type?, state?, assignee?, sprint?, tag?, parent?, include_closed = true, limit?)` (scope read) is the one search tool (#35). Code: `Features/Mcp/Tools/Orient/{SearchTool,SearchResult}.cs`; the ranking is `ISearchRanker` (`docs/search.md`).

- **With `words`** the call goes to `ISearchRanker`: an exact key first, then lexical and semantic matches fused. Titles, descriptions, tags, comments and Markdown attachment text are searched, and sprints and projects can come back too. `project` is optional and **means every project you are in when left out** (it used to default to your most recent project; that changed on purpose, and the tool description says so). `type` and `tag` (one whole tag, ignoring case) work across projects.
- **With `words`, `state`, `assignee` and `sprint` need one project**, given as `project` or implied by `parent`. Without one the answer is `validation` (field `project`) naming the projects you are in. They travel to the ranker as `Criteria`, which the ranker only accepts with exactly one project. Without words these filters keep today's behaviour: no project means your most recent one.
- **A named `state` sets `IncludeClosed`**, because the ranker drops completed-state items otherwise even when the criteria name such a state. Removed items (`state.category = 'removed'`) are never searchable, whatever you ask. `include_closed = false` drops completed items from a words search; it has no effect on a call without words.
- **Without `words`** the call is today's filter search (`OrientReads.SearchAsync`): one project (the named or implied one, else your most recent), newest change first, ILIKE-free. `limit` trims it.
- `limit` defaults to 10 with words and 25 without (today's filter-search size), and is capped at 25 either way (a larger value is clamped, not refused). `more` says there were others.
- **The result** is `{ project?, open_url, semantic, matches[], more }`. `project` is set when one project was searched; with no project, the top-level `open_url` is the issuer (the instance root). `semantic` is false when the ranker fell back to words only. Matches are items, then sprints, then projects, each with `kind`, `title`, `project` and `open_url` (item: its page; sprint: its taskboard; project: its stories backlog). An item hit adds `key`, `type`, `state`, `parent` (the nearest ancestor), `assignee`, `sprint`, `points`, `tags`, `matched_in` (`title`, `tags`, `body`, `comment` or `attachment`), `snippet` (plain text; the comment or file text for those hits) and `attachment` (the file name when an attachment matched). A sprint hit has `title` = `name: goal`, `goal` and `dates` (`yyyy-MM-dd to yyyy-MM-dd`). Filter-only hits have no `matched_in` or `snippet`.
- Tests: `Mcp/Orient/Search/SearchRoutingTests.cs` runs the tool against a `RecordingSearchRanker`; `OrientItemTests` and `SearchAttachmentTests` run the word searches end to end after `POST /api/admin/search/reindex?full=true`.

## Planning

Tools for running a plan of several agents working in parallel (#43). Code: `src/ShipLean.WebApi/Features/Mcp/{Tools/Planning,Planning}` and `src/ShipLean.Application/Features/Mcp/Planning`. The server instructions tell an agent to use ShipLean from the first idea (`planning_guide` part `work`) and to call `planning_guide` before it breaks a feature or epic down.

Agents on REST, not MCP, follow the same rule: progress, reports, decisions and brief changes for a plan belong in ShipLean (comments, task descriptions, `attach_markdown`, `report_task`), not in files under the person's home directory.

**A plan's shape.** A plan is a Feature, one Story per lane of a wave (one Story for a wave with no lanes), and one Task per agent brief. Wave, lane, model role and context size are tags, not fields (`PlanTags`): `branch:<shared branch>` on the Feature, `wave:<w>` and `lane:<l>` on a story, the story's wave and lane plus `model:<role>` and `ctx:<n>k` on a task, and `orphan` on a task made from a report's "left undone". Waves compare as dotted numbers (`0.2` < `1` < `1.1` < `2`), and stories rank in wave order. The Feature carries the plan's `## Contracts` section in its description, and `common-brief.md` and `PLAN.md` as Markdown attachments. Every read of a plan goes through `IPlanReads`: its stories are the Feature's Story children, and Removed items are left out. A story without a `wave:` tag (one made by hand) is a wave of its own after the tagged waves, in backlog order, so `plan_status`, `get_brief` and `report_task` work on a plan nobody captured. A wave is done when every story in it is Completed or Removed; the startable wave is the first wave not done.

| Tool / prompt | Scope | What it does |
|---|---|---|
| `planning_guide` | read | One part of the guide as Markdown (`part`: `work` for idea-to-done, `guide` the default for planning, or the templates brief, report, plan_reviewer, code_reviewer, fix, lead, retro) and the names of every part. With `project`, `lessons` holds the "What to change next time" section of that project's 3 newest retros, read from the Features' `retro-<yyyy-mm-dd>-<topic>.md` attachments and `Retro · ` comments. The guide is embedded in WebApi and versioned with the code. |
| `read_attachment` | read | A named attachment on a work item. Markdown up to 200 KB returns as text. Anything else (an image, a PDF, Markdown over 200 KB) returns metadata, the UI link and a download URL (`download_url`, `expires_at`, and the `command` `curl -o <file> '<url>'`), so the bytes never pass through the model ([Download URLs](#download-urls)). |
| `capture_plan` | write; preview read | A whole plan in one call: the feature (an existing one by `key`, or a new one from `title`, `goal` and `description`), `shared_branch`, `common_brief`, `plan_markdown` and the stories with their tasks (`title`, `brief`, `model`, `context_k`, `points`). Without `plan` it is a preview: the shape rules run, an error refuses it, warnings ride on it, and nothing is written. Called again with the same arguments plus the `plan` the preview returned, it writes the tree in one transaction (`IWorkItemTreeWriter`) with the claim on the plan, then, on an existing Feature, its goal, description and `branch:` tag, then the attachments. A plan the claim refuses never touches the Feature. The token seals a digest of the arguments, not the tree, so changed arguments are refused (`validation`, field `plan`); each preview applies once (`mcp_capture_plan_apply`), and a retry of the same apply returns the same plan, finishing only what is missing (the Feature patch, files the Feature lacks), so it never overwrites what a person changed since. |
| `get_brief` | read | For a task, its agent's whole prompt: the common brief, the task's brief, the shared branch and the `report` template, from the `brief` template. For a story, the lead's prompt from the `lead` template: its tasks with keys, model roles, context sizes and states, and the Feature's Contracts section. For a feature, the conductor's view: the waves in order with their stories and whether each is done (`waves`), and `startable_wave`. |
| `report_task` | write | A task's report (status `done`, `blocked` or `partial`, and the report template's fields) as a `Report · ` comment; an earlier report on the task is marked superseded. Each commit (`sha`, `subject`, `url`) and the PR become links, saved without reading the address (the cards' refresh reads them later). Each `left_undone` item is one line, since it becomes a title. The state moves by category, since states are per project: done → first Completed; blocked → unassigned and first Proposed, with the reason; partial → first InProgress if it has not started or was Completed, else unchanged. Each `left_undone` item becomes an `orphan` task under the same story. At most 25 commits and 25 left-undone items a call. |
| `plan_status` | read | The status table: waves → stories (state, assignee) → tasks with state, category, assignee, model, context size, `started_at` and `finished_at` (from revision history; finished only while Completed), commits (from the task's commit links), `review_rounds` (its `Review · ` comments) and `orphan`. Plus `startable_wave`. |
| `plan_feature` (prompt) | none | One user message that starts planning a feature (`feature`, a key) or a description (`description`). Prompts are not tool calls: they need a valid token, read scope is enough, and the tool-access gate does not apply to them; the tools the message leads to check their own scopes. |
| `research_idea` (prompt) | none | One user message to research an idea (`feature`, a key) or a description (`description`): search or create a Feature, read attachments and the log, attach findings, update the body, then `comment` and `set_status`. |

**Shape rules** (`PlanShapeRules`, checked on every preview, so a plan the apply would refuse never previews):
- a wave with no lane has exactly one story, lane names are unique within a wave, and every story has at least one task;
- `shared_branch` and `common_brief` are required. The branch is put in every brief's git commands, so it is a plain git name: letters, digits, `.`, `_`, `-` and `/`, at most 200 characters, not starting with `-`, `.` or `/`, with no `..` or `//`, and not ending with `/`, `.` or `.lock`;
- titles (feature, story, task) are one line of at most 255 characters; `lane` and `model` are short words (letters, digits, `.`, `_`, `-`; at most 64), since they become tags and land in briefs;
- a plan has at least one story; every task has a `title`, a `brief`, a `model` role and a `context_k` of at least 1, which is a warning over 100 and an error over 150;
- at most 60 items (the Feature when new, the stories and the tasks);
- sizes in UTF-8 bytes: a brief up to 10 KB, the common brief up to 64 KB, `plan_markdown` up to 200 KB, and descriptions within the Markdown render limit. A plan still has to fit in one `/mcp` body.

**Markers** (`PlanMarkers`): comments start `Report · `, `Review · ` or `Retro · `, and links are titled `commit <sha7> <subject>` or `PR <title or url>`. A reviewer records each review round as a `Review · ` comment, and a lead closes its story with `update_progress` once every task has passed review, which is what moves the plan on to the next wave.

## Feature editor tools

The tools the richer Feature editor added. Each slice that adds a tool appends its row.

| Tool | Scope | Does |
|---|---|---|
| `set_status` | write | Writes a Feature's status line (and optionally its state), stamps it verified by the caller via `set_status`, returns status, verified label and revision. |
| `create_feature` | write | Creates a Feature from title and goal, with optional epic, slug (defaults to `Slugs.FromTitle`), status and state. |
| `attach_markdown` | write | Attaches a `.md` file (name, content) to an item, stamped with the agent and `attach_markdown`; the same name replaces the old file. Its text is searchable. |
| `attach_file` | write | Attaches any file, stamped with the agent and `attach_file`. **Without `base64` it returns an upload URL** (`upload_url`, `expires_at`, and the `command` `curl -T <file> '<url>'`) and attaches nothing yet; the agent runs the command in its shell and the bytes never pass through the model. With `base64` the file is sent in the call itself. The `/mcp` body cap is the per-file limit in base64 plus 64 KB, so a file the upload form accepts fits that way too. Either way the per-file limit is checked before anything is kept, and the same name replaces the old file. `search` also matches Markdown attachment text and names the file in `attachment` (see Search). |
| `report_link_state` | write | Reports the title and state (open, draft, merged, closed) of an address ShipLean can't read, as of a time. Applies to every link with that exact address in projects you are in; the card shows it as reported by the agent. `not_found` when no link matches. |
| `link` | write | (Feature editor addition) `url` links take optional `title` and `note`, and go through the link-card checks: `link_invalid_url`, `link_duplicate`. A URL link no longer bumps the item's revision. |

### Upload URLs

`attach_file` without `base64` checks the item (Member), the name and the content type, then issues a token and replies with the URL. The reply's `upload_url` is absolute, built like every other link the MCP returns. When `base_url` is set, that is the host. When it is blank, the host is the one the request came in on: the last `X-Forwarded-Host` from a trusted proxy (and `X-Forwarded-Proto` for the scheme), unless that name is a cluster service (`*.svc`). Otherwise it is the request's own host. `Request.Host` is not rewritten, so a redirect still uses the app's own host.

- **Token:** 32 random bytes, base64url. Only its SHA-256 is stored (`transfer_token`, with the item, file name, content type, user, agent stamp and agent authorization). It lives 10 minutes.
- **`PUT /api/uploads/{token}`** takes the raw bytes as the body, so `curl -T file.png '<url>'` needs no headers, cookie or bearer, and the `X-ShipLean-Client` guard doesn't apply. The content type given to `attach_file` wins over whatever the client sends. It is rate limited per client address (60 a minute).
- **Runs as the user who asked for the URL.** Member is checked again on use, and the URL stops working if the agent is revoked, the user is disabled or the member is removed.
- **Limits** are the web upload's: `SHIPLEAN_ATTACHMENT_MAX_BYTES` per file and the per-item total, enforced while the body streams; SVG is sanitised. The `/mcp` body cap is that same per-file limit written as base64, plus 64 KB.
- **One use.** The token is claimed before the body is read, so of two concurrent PUTs only one stores (the other is 404). If the upload then fails (too big, dropped connection, item full), the claim is released and the same URL can be retried until it expires.
- **Answers:** 200 with `{ id, name, sizeBytes, replaced }`. An unknown, expired, spent or no-longer-allowed token is 404, and the response never says which.
- Behind a proxy, `/api/uploads/` must be skipped like `/mcp` ([`auth.md`](auth.md#paths-the-proxy-must-skip)).

### Download URLs

`read_attachment` on a file it cannot return as text (any non-Markdown file, or Markdown over 200 KB) issues a download token for that attachment and adds `download_url` (absolute, built like `upload_url`), `expires_at` and `command` to its reply. Small Markdown keeps returning its text and no URL. This is how an agent fetches a pasted screenshot: run the command, then read the saved file.

- **Token:** the same `transfer_token` table and 10-minute life as upload URLs, kind `download`, tied to the attachment id. It is never spent: it works for any number of GETs until it expires.
- **`GET /api/downloads/{token}`** needs no header, cookie or bearer, and is rate limited per client address (60 a minute). It goes through `AttachmentStore.OpenServedAsync` and the same `AttachmentFileResult` as `GET /api/attachments/{id}/content`, so the content type, `Content-Disposition` (inline only for stored image types), `nosniff` and CSP headers are identical.
- **Runs as the user who asked for the URL.** Member is checked again on use; the URL stops working if the agent is revoked, the user is disabled or the member is removed.
- **Answers:** the file, or 404 for an unknown or expired token, an attachment archived or replaced since the URL was issued, or a user no longer allowed. The response never says which.
- Behind a proxy, `/api/downloads/` must be skipped like `/api/uploads/` ([`auth.md`](auth.md#paths-the-proxy-must-skip)).

## Adding a tool

1. A class marked `[McpServerToolType]`, **internal**, under `Features/Mcp/Tools/<Area>/`, with `[McpScope]` on each tool or on the class.
2. No Dapper and no Npgsql in the tool: it calls the contracts (`IWorkItemStore`, `IProjectAccess`, ...) or a read in `Application/Features/Mcp/<Area>/`.
3. Do not catch `ApiException`.
4. A sweep-rows file `WebApi.Tests/Mcp/<Area>/<Area>McpRows.cs` implementing `IMcpSweepRows`, with a row per tool. `McpAccessSweepTests` fails for a tool without one, and calls each row as Joel; a row's arguments name an entity in the seeded `PART` project (Joel is not a member), and the call must answer `not_found`.
5. Tests go through `McpTestClient` and `ShipLeanHost.IssueAgentToken`. A tool that only the tests need is registered through `ConfigureTestServices` (`services.AddMcpServer().WithTools<T>()`), never in production code.

The server also sends `instructions` (naming rules, previews, errors): `McpServerServices.Instructions`.
