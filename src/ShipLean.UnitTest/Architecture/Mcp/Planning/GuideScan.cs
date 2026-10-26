using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using ShipLean.WebApi;
using ShipLean.WebApi.Features.Mcp.Planning;

namespace ShipLean.UnitTest.Architecture.Mcp.Planning;

// Scans of the planning guide's files, which ship as embedded resources named planning-guide/<file>.md.
public static partial class GuideScan
{
    private const string McpNamespace = "ShipLean.WebApi.Features.Mcp";

    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Names that are also ordinary words (a text cursor, a magnum opus, to grok, a task tool): matched whole-word and
    // case-sensitively in their capitalised form, so the registered result field `cursor` and plain English pass.
    private static readonly string[] BannedCapitalised = ["Cursor", "Composer", "Opus", "Grok", "Mistral", "Devin", "Aider", "Cline", "Windsurf", "Copilot", "Gemma", "Llama", "Qwen"];

    // Unambiguous names, matched whole-word and case-insensitively: vendors, model families, client tool names and paths.
    private static readonly string[] BannedAnyCase =
    [
        "Claude", "Anthropic", "Sonnet", "Haiku", "OpenAI", "ChatGPT", "GPT", "Gemini", "DeepSeek", "AskQuestion", "subagent_type", "~/.cursor",
    ];

    // The client's sub-agent tool, matched only with a capital T.
    private const string BannedTaskTool = "Task tool";

    [GeneratedRegex("`([^`\n]+)`")]
    private static partial Regex Backticked();

    [GeneratedRegex("^[a-z]+(_[a-z]+)+$")]
    private static partial Regex SnakeCase();

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex WordBreak();

    // file name -> text, for every guide file in the WebApi assembly.
    public static IReadOnlyDictionary<string, string> GuideFiles()
    {
        var assembly = typeof(ApiMarker).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("planning-guide/", StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .ToDictionary(name => name, name => Read(assembly, name));
    }

    // "file: `token`" for every backticked snake_case token that is not in the allowed set.
    public static IReadOnlyList<string> UnregisteredTokens(IReadOnlyDictionary<string, string> files, IReadOnlyList<string> allowed)
        => files
            .SelectMany(file => Backticked().Matches(file.Value)
                .Select(m => m.Groups[1].Value)
                .Where(token => SnakeCase().IsMatch(token) && !allowed.Contains(token))
                .Select(token => $"{file.Key}: `{token}`"))
            .Order(StringComparer.Ordinal)
            .ToList();

    // "file: <name>" for every banned name a file mentions.
    public static IReadOnlyList<string> BannedMentions(IReadOnlyDictionary<string, string> files)
        => files
            .SelectMany(file => Mentions(file.Value, BannedCapitalised, RegexOptions.None)
                .Concat(Mentions(file.Value, BannedAnyCase, RegexOptions.IgnoreCase))
                .Concat(Mentions(file.Value, [BannedTaskTool], RegexOptions.None))
                .Select(name => $"{file.Key}: {name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<string> Mentions(string text, IEnumerable<string> names, RegexOptions options)
        => names.Where(name => Regex.IsMatch(text, $@"(?<![\w]){Regex.Escape(name)}(?![\w])", options));

    // Tool names, prompt names, tool parameter names, every [JsonPropertyName] on a type under Features.Mcp, and the wire values of the enums those types use.
    // The MCP transport writes enums as their member names unless [JsonStringEnumMemberName] says otherwise, and the guide's part names.
    public static IReadOnlyList<string> RegisteredNames()
    {
        var types = typeof(ApiMarker).Assembly.GetTypes();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(Members))
            {
                var tool = method.GetCustomAttribute<McpServerToolAttribute>();
                var prompt = method.GetCustomAttribute<McpServerPromptAttribute>();
                if (tool is null && prompt is null)
                {
                    continue;
                }
                names.Add(tool?.Name ?? prompt?.Name ?? SnakeCaseOf(method.Name));
                foreach (var parameter in method.GetParameters().Where(p => p.Name is not null))
                {
                    names.Add(parameter.Name!);
                    names.Add(SnakeCaseOf(parameter.Name!));
                }
            }
        }

        foreach (var part in Enum.GetValues<PlanGuidePart>())
        {
            names.Add(part.WireName());
        }

        var enums = new HashSet<Type>();
        foreach (var type in types.Where(t => (t.Namespace ?? string.Empty).StartsWith(McpNamespace, StringComparison.Ordinal)))
        {
            foreach (var property in type.GetProperties(Members))
            {
                var wire = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                if (wire is not null)
                {
                    names.Add(wire.Name);
                }
                foreach (var used in Unwrap(property.PropertyType).Where(t => t.IsEnum))
                {
                    enums.Add(used);
                }
            }
        }

        foreach (var member in enums.SelectMany(e => e.GetFields(BindingFlags.Public | BindingFlags.Static)))
        {
            names.Add(member.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? member.Name);
        }

        return names.Order(StringComparer.Ordinal).ToList();
    }

    // The type itself plus every generic argument and element type, nullable stripped.
    private static IEnumerable<Type> Unwrap(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        yield return type;
        var inner = type.IsArray ? [type.GetElementType()!] : type.GetGenericArguments();
        foreach (var nested in inner.SelectMany(Unwrap))
        {
            yield return nested;
        }
    }

    private static string SnakeCaseOf(string name) => WordBreak().Replace(name, "_").ToLowerInvariant();

    private static string Read(Assembly assembly, string name)
    {
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        return reader.ReadToEnd();
    }
}
