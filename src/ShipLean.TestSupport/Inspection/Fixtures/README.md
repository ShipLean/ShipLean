# Inspection fixtures

Each fixture is a pair: `<name>.otlp.json` (OTLP/JSON a collector receives: `resourceSpans` and `resourceLogs`, one entry per collector batch) and `<name>.expected.json` (`{ compareKeys, run }`: the `RawRun` a store must return, normalised as K3 says). Times are anchored at 2026-11-10T10:00:00Z; loaders rebase them and `RawRunComparer` compares offsets from the run start. `<name>.assembled.json` is the `AssembledRun` the K4 assembler must produce from `run`.

Summary rules the expected files pin: `models` is the distinct `model` attributes; `tokensIn`/`tokensOut` sum `input_tokens`/`output_tokens` on spans when any span has them, else on logs, else null; `errorCount` is error spans plus ERROR logs; `links` follows `LinkRules` (one per item, Tagged, then ShipleanFile, then FromToolCall).

Assembly rules the assembled files pin:
- Traced runs: a turn per `claude_code.interaction` root span (prompt text from the `claude_code.user_prompt` log with the same prompt id). Rows are the root's descendants except `claude_code.tool.blocked_on_user` (becomes the tool row's wait) and `claude_code.tool.execution` (dropped). Depth is below the root. Kind: `claude_code.llm_request` is ModelCall, a ShipLean tool is ShipleanTool, `claude_code.subagent` is Subagent (its descendants are dropped and counted in CollapsedChildCount), anything else Tool. IsRetry is an `attempt` above 1. A tool row is an error when its `tool.execution` child failed, and its error marker says that span's status message. Logs attach to a row when they carry its span id or the same `tool_use_id` as its span (Claude Code's `tool_result` and `tool_decision` records point at the interaction or the permission wait, not the tool). A turn's prompt id is the interaction span's, else the one its logs carry (Claude Code's spans have none); logs with no span id join the interaction span, which has no row.
- Log-only runs: turns group by prompt id in time order; one row per log record (`log-<n>`), Event (ShipleanTool for a ShipLean tool); a record with `duration_ms` ends at its time and starts that long before it, otherwise it has no end.
- Markers: Wait per permission wait, Error per error row or ERROR record, ShipleanTool per ShipLean call, sorted by time. `WhatItDid` is the last `report_task` or `hand_back` call: `report_task: done · 1 commit`, never prompt text.

Comparison rules: `compareKeys` lists only keys a Loki reader keeps exactly: the K1 mapped fields (`session.id`, `prompt.id`, `shiplean.item`, `shiplean.project`, which Loki maps back to dotted form) and attributes whose names have no dots. Other dotted keys (`service.name`, `user.email`, `shiplean.link_source`, `event.name`, `host.name`) are not listed: Loki returns them underscored, and Agent, StartedBy and the ShipleanFile link are compared through the summary. Values compare ordinally in the K3 form (`1.5`, `true`, arrays as compact JSON); span events compare by name, offset and listed attributes. `claude-code-synthetic` covers a double (`temperature`), an array (`allowed_tools`), a record attribute that clashes with a resource one (`team`: the record's `infra` wins over the resource's `platform`, on the `pick_up` span) and a span event (`first_token` on the first `llm_request`).

## Assembly rules

Decisions the C1 assembler (`RunAssembler`) takes where the pinned files are silent. Later lanes build on them.
- Row `Name` is the `tool_name` attribute, else the span name. `Input` is the first of `tool_parameters`, `tool_input`, `input`. `Output` is the span's `output` attribute, else the first row log's `output`.
- Direct children of the interaction have `ParentId` null and `Depth` 0.
- The ShipleanTool marker `At` is the span start in traced runs and the record time in log-only runs.
- An error marker's label is the span `StatusMessage` (traced) or the record `Body` (log-only).
- Recorded runs (`Recorded/`, real agent output) are opted in per store suite and pin their own `assembled.json`; only Claude Code has one (D27). Prompt text comes only from events ending `user_prompt` (Cursor's `beforeSubmitPrompt` gives none).
- `FilePath` is set only for Edit, MultiEdit, Write, NotebookEdit, apply_patch, or `*FileEdit` events. `CommitSha` comes from the `commit_sha` attribute.
- `WhatItDid` is `<tool>: <status> · N commit(s)`, with no commit part when there are none.
- Duplicates: spans are de-duplicated by span id (first by start, then end) and identical log records are collapsed, so a collector retry changes nothing and input order does not matter.
- Logs from non-agent services (a test runner) in a traced run: a log with no row's span id that joins no interaction becomes a top-level Event row in the turn whose time window holds it; outside every turn it is not shown. In log-only runs every record is already an Event row.
- A span whose parent is missing joins the turn with the same prompt id as a top-level row; with no matching turn it becomes its own turn. So a prompt id can appear on more than one turn only when no interaction span carries it.
- Content hiding (C2): the content also sits in `RunRow.Attributes` (`tool_parameters`, `tool_input`, `input`, `output`) and in row logs' attributes (`prompt`, `output`); strip those keys too when content is hidden.
