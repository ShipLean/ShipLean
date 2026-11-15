using System.Text;
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
        var headers = string.IsNullOrWhiteSpace(i.Headers) ? "" : $"export OTEL_EXPORTER_OTLP_HEADERS=\"{i.Headers}\"\n";
        var env = $"""
            # Claude Code telemetry to your collector.
            export CLAUDE_CODE_ENABLE_TELEMETRY=1
            export CLAUDE_CODE_ENHANCED_TELEMETRY_BETA=1   # unverified: traces are a beta; the switch may change
            export OTEL_TRACES_EXPORTER=otlp
            export OTEL_LOGS_EXPORTER=otlp
            export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
            export OTEL_EXPORTER_OTLP_ENDPOINT="{i.EndpointOrPlaceholder}"
            {headers}export OTEL_RESOURCE_ATTRIBUTES="{i.ResourceAttributes}"
            # Optional, percent-encoded: add ,{i.Fields.Repo}=<repo url>,{i.Fields.Branch}=<branch> to OTEL_RESOURCE_ATTRIBUTES (agent-relay does this for you).
            # Content stays off. Uncomment to send it (see the note on secrets).
            # export OTEL_LOG_USER_PROMPTS=1
            # export OTEL_LOG_TOOL_DETAILS=1
            # export OTEL_LOG_TOOL_CONTENT=1                # unverified name

            """;
        var settings = new StringBuilder("{\n  \"env\": {\n");
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
        pairs.Add(("OTEL_RESOURCE_ATTRIBUTES", i.ResourceAttributes));
        settings.Append(string.Join(",\n", pairs.Select(p => $"    {Json(p.Item1)}: {Json(p.Item2)}"))).Append("\n  }\n}\n");

        return new AgentSetupDto("claude-code", label,
        [
            new AgentSetupFileDto("telemetry.env", "bash", env),
            new AgentSetupFileDto(".claude/settings.json", "json", settings.ToString()),
        ],
        [
            "Use either the env block (per user or per run) or the settings.json file (per repository); not both.",
            $"The item tag is in OTEL_RESOURCE_ATTRIBUTES. Change {i.Fields.Item} to the item you start the run for, or let agent-relay set it.",
            ContentNote,
        ],
        Docs("claude-code"));
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
            "");
        var perRun = $"""
            # Per run: tag it with the item (unverified: Codex CLI reading OTEL_RESOURCE_ATTRIBUTES)
            export OTEL_RESOURCE_ATTRIBUTES="{i.ResourceAttributes}"
            # then start Codex CLI as usual in this shell

            """;
        return new AgentSetupDto("codex-cli", label,
        [
            new AgentSetupFileDto("config.toml", "toml", config),
            new AgentSetupFileDto("run.sh", "bash", perRun),
        ],
        [
            "config.toml sets where Codex CLI sends its events; run.sh tags one run with the project and item.",
            "Codex CLI sends log events (codex.*) and, in recent releases, may send traces too; the run page shows what arrives.",
            ContentNote,
        ],
        Docs("codex-cli"));
    }

    private static AgentSetupDto Cursor(string label, Inputs i)
    {
        var shiplean = $"item={i.ItemKey}\nproject={i.ProjectKey}\n";
        var hookHeaders = string.IsNullOrWhiteSpace(i.Headers) ? "" : $"export SHIPLEAN_INSPECTION_AGENT_OTLP_HEADERS=\"{i.Headers}\"\n";
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
            "The .shiplean file in the repository root tags the run with its item; change it per task. SHIPLEAN_ITEM overrides it.",
            ContentNote,
        ],
        Docs("cursor"));
    }

    private static string Docs(string agent) => $"https://docs.shiplean.app/inspection/{agent}";

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
