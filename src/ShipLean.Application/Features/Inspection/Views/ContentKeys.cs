namespace ShipLean.Application.Features.Inspection.Views;

// What a project that hides content (D4) never sends: prompt text, tool input and output, and the attribute keys that carry them. The
// assembler leaves these in RunRow.Attributes and in each row log's attributes, so every view strips them there too.
public static class ContentKeys
{
    public static readonly IReadOnlySet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "prompt", "prompt_text", "input", "tool_input", "tool_parameters", "tool_output", "output", "response", "result", "content", "command", "arguments",
        // Claude Code with prompt or tool capture on: the interaction span's prompt, a Bash span's whole command line, and what
        // OTEL_LOG_USER_PROMPTS adds to llm_request spans (system_prompt_preview, new_context, response.model_output).
        "user_prompt", "full_command", "system_prompt_preview", "new_context", "model_output",
    };

    // Under the OpenTelemetry GenAI prefix, a segment that starts with one of these holds messages or prompt text (gen_ai.prompt.0.content,
    // gen_ai.input.messages, gen_ai.system_instructions). Token counts (gen_ai.usage.input_tokens) and model names stay visible.
    private static readonly string[] GenAiContentSegments = ["prompt", "completion", "message", "system_instruction"];

    // A key is content when it, or its last dotted segment, is listed (so foo.prompt and tool.input go), or when it is a GenAI content key.
    public static bool IsHidden(string key)
    {
        var segments = key.Split('.');
        return Hidden.Contains(segments[^1])
            || (segments[0].Equals("gen_ai", StringComparison.OrdinalIgnoreCase)
                && segments.Skip(1).Any(s => GenAiContentSegments.Any(c => s.StartsWith(c, StringComparison.OrdinalIgnoreCase))));
    }

    public static IEnumerable<KeyValuePair<string, string>> Visible(IReadOnlyDictionary<string, string> attributes, bool contentShown)
        => contentShown ? attributes : attributes.Where(a => !IsHidden(a.Key));
}
