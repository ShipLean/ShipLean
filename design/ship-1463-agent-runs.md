# SHIP-1463: Agent runs (viewing traces and logs on the Task page)

Mock-up, not built. Decisions D14–D22 settled by Joel, 14 Nov 2026. Screen: [`screens/agent-runs.html`](screens/agent-runs.html). Store modes, linking and content display are decided on SHIP-1463 (D1–D13); this file covers only the screens and their decisions D14–D22.

ShipLean reads runs from the team's ClickHouse or Tempo + Loki through `ITraceSource`. It never writes telemetry.

**Naming.** "OpenAI CLI" here is the OpenAI command-line coding agent that SHIP-1463 names. Its product name, and the namespace prefix on its event names, can't appear in this repository (`NoForeignProductNameTests`), so the mock-up shows its events without the prefix (`tool_result`, `api_request`). How the build matches those event names is decision D23 on SHIP-1463.

Images: `screens/agent-runs*.png` (1440 × 900) in light and dark; the variants are in `screenshots.sh`. `screens/agent-runs-phone-*.png` (390 × 844) show the narrow-screen states, taken with headless Chrome at `--window-size=390,844` because `screenshots.sh` can't shoot that narrow.

## Task page: Agent runs section

Under **Changes** (SHIP-1460), above Discussion. Newest run first. Shown only when the instance and the project have Inspection on.

| Column | What it shows |
|---|---|
| Agent | Claude Code, OpenAI CLI or Cursor; who started it and when |
| What it did | From the run's ShipLean MCP calls only (`report_task: done · 3 commits`, `hand_back: blocked …`), never prompt text (D17). Under it: how the run was linked (**Tagged** = `shiplean.item` on the run; **From pick_up** = derived from a ShipLean tool call; **.shiplean file** = Cursor's hook script), turns, spans or events, model |
| Took | Wall-clock length of the run |
| Timeline | Mini strip: turns, permission waits (striped), errors (red ticks), ShipLean tool calls (dots) |
| Tokens | As the agent reports them; `—` when it reports none. No cost or price anywhere in v1 (D21) |
| Errors | Spans or events with an error status |

Header: run count, total time, tokens, error count, total permission wait. Footer: which store, its retention, whether content is shown, link to Agent setup.

States (`?view=task`): default (3 runs), `&state=empty` (no runs; how runs get linked; setup links per agent), `&state=down` (store unreachable; only this section says so).

## Run page

`/p/SHIP/items/SHIP-171/runs/<run>`, full width like Changes (D14). `?view=run`.

- **Header**: agent, start, who, link source; Older / Newer between the item's runs; totals (took, turns, spans or events, tokens in/out, errors, permission waits); All / Errors only; find a span.
- **Overview strip**: the whole run, turns as blocks, idle gaps grey, waits striped, errors red. Click a turn to zoom the axis to it; **Whole run** goes back (D18).
- **Waterfall**: grouped by turn (the turn row shows its prompt when content is shown), indented by parent, one bar per span placed on the axis. Kinds: model call, tool, ShipLean MCP tool, subagent (collapsed with a span count), permission wait (striped part of the tool's bar). Error spans have a ✕ and a red bar; a retried API error a dashed bar.
- **Detail panel**: kind, start, duration, offset into the run, wait; Copy link, Open in Grafana. Then **Open in Changes** for edits (file + commit, SHIP-1460), input, output (first 40 lines), attributes, joined log records (by span id; by conversation and time for log-only runs).
- **Content hidden** (project default, D4): turn rows say "prompt hidden", tool rows show the tool name only, and Input/Output become one note. Timings, attributes and logs still show.
- **Log-only runs** (OpenAI CLI log events, Cursor hook events; D19): same page, flat rows grouped by turn, a bar drawn back from the event by `duration_ms`, a diamond for events with no duration, and a banner saying what the agent doesn't send.

States: default (`run=claude`, failed test selected), `&state=zoom|errors|permission|edit|hidden|truncated|expired`, `&run=openai`, `&run=cursor`.

Span and event names follow what each agent sends as far as we knew on 14 Nov 2026; lane 1c's recorded runs replace the sample data.

## Narrow screens

Desktop only (D15): under 900px no section, and a run link shows one line and a link back. The phone app shows nothing in v1.

## Decisions (Joel, 14 Nov 2026)

| # | Question | Answer |
|---|---|---|
| D14 | Where do runs show? | Section on the item page under Changes + full-width Run page |
| D15 | Narrow screens and the phone app? | Desktop only in v1, like Changes |
| D16 | Which items show Agent runs? | Any item runs link to directly; no roll-up of children |
| D17 | What does a run row say it did? | Only what its ShipLean tool calls say; never prompt text |
| D18 | Waterfall layout? | By turn, one axis for the run, overview strip with click-to-zoom, subagents collapsed |
| D19 | Log-only runs? | Same page, flat event rows, banner on what's missing |
| D20 | Comments on spans in v1? | **Changed: later, not v1** (was: yes, SHIP-1460's comment model) |
| D21 | Cost? | **Changed: no cost or price in v1** (was: what the agent reports) |
| D22 | Measuring? | Section seen with runs, run opened, span selected, zoom, errors only, Open in Changes; weekly, against Task views of tasks with runs and next to Changes views |

## Screens

| State | Light | Dark |
|---|---|---|
| Task page · Agent runs (`?view=task`) | ![](screens/agent-runs-task.light.png) | ![](screens/agent-runs-task.dark.png) |
| Task page · no runs (`&state=empty`) | ![](screens/agent-runs-empty.light.png) | ![](screens/agent-runs-empty.dark.png) |
| Task page · store unreachable (`&state=down`) | ![](screens/agent-runs-down.light.png) | ![](screens/agent-runs-down.dark.png) |
| Run page · failed test selected (`?view=run`) | ![](screens/agent-runs.light.png) | ![](screens/agent-runs.dark.png) |
| Zoomed to Turn 1 (`&state=zoom`) | ![](screens/agent-runs-zoom.light.png) | ![](screens/agent-runs-zoom.dark.png) |
| Errors only (`&state=errors`) | ![](screens/agent-runs-errors.light.png) | ![](screens/agent-runs-errors.dark.png) |
| 21-minute permission wait (`&state=permission`) | ![](screens/agent-runs-permission.light.png) | ![](screens/agent-runs-permission.dark.png) |
| Edit linked into Changes (`&state=edit`) | ![](screens/agent-runs-edit.light.png) | ![](screens/agent-runs-edit.dark.png) |
| Content hidden (`&state=hidden`) | ![](screens/agent-runs-hidden.light.png) | ![](screens/agent-runs-hidden.dark.png) |
| OpenAI CLI, log events (`&run=openai`) | ![](screens/agent-runs-openai.light.png) | ![](screens/agent-runs-openai.dark.png) |
| Cursor, hook events (`&run=cursor`) | ![](screens/agent-runs-cursor.light.png) | ![](screens/agent-runs-cursor.dark.png) |
| Trace truncated (`&state=truncated`) | ![](screens/agent-runs-truncated.light.png) | ![](screens/agent-runs-truncated.dark.png) |
| Run no longer available (`&state=expired`) | ![](screens/agent-runs-expired.light.png) | ![](screens/agent-runs-expired.dark.png) |
| Phone · Task page (390) | ![](screens/agent-runs-phone-task.light.png) | ![](screens/agent-runs-phone-task.dark.png) |
| Phone · run link (390) | ![](screens/agent-runs-phone-run.light.png) | ![](screens/agent-runs-phone-run.dark.png) |
