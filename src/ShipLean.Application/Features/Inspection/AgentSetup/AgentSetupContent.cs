using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.AgentSetup;

// The per-agent files and notes in one place (D24, D29). Each agent is one method: edit the lines here when a real run shows a
// different name. Only the collector endpoint, its agent-facing headers, the project and the item go in; never a store connection.
// Lines marked "unverified" are written from each agent's docs and not yet confirmed by a recorded run.
public static class AgentSetupContent
{
    public const string ItemPlaceholder = "<item-key>";
    public const string EndpointPlaceholder = "<your collector, e.g. https://otel.example.com:4318>";

    private const string ContentNote = "Prompts, tool input and tool output stay off in the lines below. Turning them on sends them to your collector; they can hold secrets, so redact there (project setting: show content).";

    public sealed record Inputs(string ProjectKey, string ItemKey, string? Endpoint, string? Headers, InspectionFieldNames Fields)
    {
        public string EndpointOrPlaceholder => string.IsNullOrWhiteSpace(Endpoint) ? EndpointPlaceholder : Endpoint.TrimEnd('/');

        public string ResourceAttributes => $"{Fields.Project}={Uri.EscapeDataString(ProjectKey)},{Fields.Item}={(ItemKey == ItemPlaceholder ? ItemKey : Uri.EscapeDataString(ItemKey))}";
    }

    public static AgentSetupDto For(string agent, string label, Inputs inputs) => agent switch
    {
        "claude-code" => ClaudeCode(label, inputs),
        "codex-cli" => OpenAiCli(label, inputs),
        "cursor" => Cursor(label, inputs),
        _ => throw new ArgumentOutOfRangeException(nameof(agent), agent, "Unknown agent."),
    };

    private static AgentSetupDto ClaudeCode(string label, Inputs i)
    {
        var headers = string.IsNullOrWhiteSpace(i.Headers) ? "" : $"export OTEL_EXPORTER_OTLP_HEADERS={ShellQuote(i.Headers)}\n";
        var env = $"""
            # Claude Code telemetry to your collector.
            export CLAUDE_CODE_ENABLE_TELEMETRY=1
            export CLAUDE_CODE_ENHANCED_TELEMETRY_BETA=1
            export OTEL_TRACES_EXPORTER=otlp
            export OTEL_LOGS_EXPORTER=otlp
            export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
            export OTEL_EXPORTER_OTLP_ENDPOINT="{i.EndpointOrPlaceholder}"
            {headers}export OTEL_RESOURCE_ATTRIBUTES="{i.ResourceAttributes}"
            # Optional, percent-encoded: add ,{i.Fields.Repo}=<repo url>,{i.Fields.Branch}=<branch> to OTEL_RESOURCE_ATTRIBUTES (agent-relay does this for you).
            # Content stays off. Uncomment to send it (see the note on secrets).
            # export OTEL_LOG_USER_PROMPTS=1
            # export OTEL_LOG_ASSISTANT_RESPONSES=0         # OTEL_LOG_USER_PROMPTS=1 also captures replies unless this is 0
            # export OTEL_LOG_TOOL_DETAILS=1                # also lets a run with no item tag link by its ShipLean tool calls
            # export OTEL_LOG_TOOL_CONTENT=1                # tool input and output; OTEL_LOG_RAW_API_BODIES=1 sends everything

            """;

        return new AgentSetupDto("claude-code", label,
        [
            new AgentSetupFileDto("telemetry.env", "bash", env),
            new AgentSetupFileDto("shiplean-telemetry.json", "json", ClaudeSettings(i, withItem: true)),
            new AgentSetupFileDto("user-claude-settings.json", "json", ClaudeSettings(i, withItem: false)),
        ],
        [
            "Claude Code reads its telemetry settings from the environment, a --settings file or your user settings (~/.claude/settings.json). A repository's .claude/settings.json cannot turn telemetry on: Claude Code ignores the exporter variables there.",
            "Per run: start Claude Code with --settings shiplean-telemetry.json, or source telemetry.env first. Keep shiplean-telemetry.json out of the repository: it holds the collector token.",
            "user-claude-settings.json is for ~/.claude/settings.json (merge its env block). It leaves out the item, because user settings apply to every run; tag a run with the item through the per-run file or OTEL_RESOURCE_ATTRIBUTES.",
            $"Change {i.Fields.Item} in the per-run file to the item you start the run for, or let agent-relay set it. Tagging is the dependable link: without it a run links only from ShipLean tool calls (OTEL_LOG_TOOL_DETAILS=1), and only after the call names the item.",
            "The collector token is shared by the whole instance. Never commit it; rotate it if it leaks.",
            ContentNote,
        ],
        Docs("claude-code"));
    }

    private static string ClaudeSettings(Inputs i, bool withItem)
    {
        var pairs = new List<(string, string)>
        {
            ("CLAUDE_CODE_ENABLE_TELEMETRY", "1"),
            ("CLAUDE_CODE_ENHANCED_TELEMETRY_BETA", "1"),
            ("OTEL_TRACES_EXPORTER", "otlp"),
            ("OTEL_LOGS_EXPORTER", "otlp"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", i.EndpointOrPlaceholder),
        };
        if (!string.IsNullOrWhiteSpace(i.Headers))
        {
            pairs.Add(("OTEL_EXPORTER_OTLP_HEADERS", i.Headers));
        }
        pairs.Add(("OTEL_RESOURCE_ATTRIBUTES", withItem ? i.ResourceAttributes : $"{i.Fields.Project}={Uri.EscapeDataString(i.ProjectKey)}"));
        return "{\n  \"env\": {\n" + string.Join(",\n", pairs.Select(p => $"    {Json(p.Item1)}: {Json(p.Item2)}")) + "\n  }\n}\n";
    }

    private static AgentSetupDto OpenAiCli(string label, Inputs i)
    {
        var endpoint = i.EndpointOrPlaceholder;
        var headers = TomlHeaders(i.Headers);
        var config = string.Join('\n',
            "# ~/.codex/config.toml",
            "[otel]",
            "environment = \"shiplean\"",
            "log_user_prompt = false   # content off; set true to send prompt text (see the note on secrets)",
            "exporter = { otlp-http = { endpoint = \"" + endpoint + "/v1/logs\", protocol = \"binary\"" + headers + " } }",
            "trace_exporter = { otlp-http = { endpoint = \"" + endpoint + "/v1/traces\", protocol = \"binary\"" + headers + " } }   # unverified: trace export",
            "",
            "[otel.span_attributes]   # documented route for tags; spans only",
            "\"shiplean.project\" = " + Json(i.ProjectKey),
            "");
        var perRun = $"""
            # Usage: bash run.sh <your Codex CLI command> [args]
            # Tags this one run with the item, then starts the command you gave.
            export OTEL_RESOURCE_ATTRIBUTES="{i.ResourceAttributes}"   # unverified: Codex CLI may not read this variable
            cmd="$1"; shift
            exec "$cmd" -c 'otel.span_attributes."shiplean.item"="{i.ItemKey}"' "$@"

            """;
        return new AgentSetupDto("codex-cli", label,
        [
            new AgentSetupFileDto("config.toml", "toml", config),
            new AgentSetupFileDto("run.sh", "bash", perRun),
        ],
        [
            "config.toml sets where Codex CLI sends its events; run.sh tags one run with the project and item.",
            "Tags go in through [otel.span_attributes] (spans only). Log events may stay untagged until a real run shows otherwise; the OTEL_RESOURCE_ATTRIBUTES line in run.sh is unverified.",
            "Codex CLI sends log events (codex.*) and, in recent releases, may send traces too; the run page shows what arrives.",
            ContentNote,
        ],
        Docs("codex-cli"));
    }

    private static AgentSetupDto Cursor(string label, Inputs i)
    {
        var shiplean = $"item={i.ItemKey}\nproject={i.ProjectKey}\n";
        var hookHeaders = string.IsNullOrWhiteSpace(i.Headers) ? "" : $"export SHIPLEAN_INSPECTION_AGENT_OTLP_HEADERS={ShellQuote(i.Headers)}\n";
        var env = $"""
            # Where the hook posts. Set these where Cursor starts (shell profile or the OS environment).
            export SHIPLEAN_INSPECTION_AGENT_OTLP_ENDPOINT="{i.EndpointOrPlaceholder}"
            {hookHeaders}# Content stays off. Uncomment to send prompts, tool input and output.
            # export SHIPLEAN_LOG_CONTENT=1

            """;
        return new AgentSetupDto("cursor", label,
        [
            new AgentSetupFileDto(".cursor/hooks.json", "json", Resource("cursor/hooks.json")),
            new AgentSetupFileDto(".cursor/hooks/shiplean-cursor-hook.mjs", "javascript", Resource("cursor/shiplean-cursor-hook.mjs")),
            new AgentSetupFileDto(".shiplean", "ini", shiplean),
            new AgentSetupFileDto("hook.env", "bash", env),
        ],
        [
            "Cursor has no native telemetry export, so a hook script turns its hook events (shell, MCP call, file edit, stop) into log records for your collector. Needs Node 18 or newer.",
            "Cursor does not send model-call timing or token counts; the run page shows events only.",
            "Known gap: Cursor sends bare MCP tool names and keeps the item key only in tool input, so runs are linked by tagging (.shiplean or SHIPLEAN_ITEM), not by tool calls.",
            "The hook sends the user's email from Cursor's payload, and the repository URL and branch from git when it can read them.",
            "The .shiplean file in the repository root tags the run with its item; change it per task. SHIPLEAN_ITEM overrides it.",
            ContentNote,
        ],
        Docs("cursor"));
    }

    private static string Docs(string agent) => $"https://docs.shiplean.app/inspection/{agent}";

    private static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static string Json(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    private static string TomlHeaders(string? headers)
    {
        var pairs = (headers ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2 && p[0].Length > 0)
            .Select(p => $"{Json(p[0].Trim())} = {Json(p[1].Trim())}")
            .ToList();
        return pairs.Count == 0 ? "" : $", headers = {{ {string.Join(", ", pairs)} }}";
    }

    private static string Resource(string name)
    {
        var assembly = typeof(AgentSetupContent).Assembly;
        var resource = $"inspection/agent-setup/{name}";
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource {resource} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
