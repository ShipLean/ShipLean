using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Assembly;

// D17: what a run says it did comes only from its last report_task or hand_back call to ShipLean, never from prompt text.
public static class WhatItDid
{
    private static readonly string[] Reporting = ["report_task", "hand_back"];

    // "pick_up" from "mcp__shiplean__pick_up" or "shiplean__pick_up".
    public static string ShortName(string toolName)
    {
        var name = toolName.StartsWith("mcp__", StringComparison.Ordinal) ? toolName["mcp__".Length..] : toolName;
        return name.StartsWith("shiplean__", StringComparison.Ordinal) ? name["shiplean__".Length..] : name;
    }

    public static string? From(IEnumerable<RunRow> rows)
    {
        var call = rows
            .Where(row => row.Kind == RowKind.ShipleanTool && Reporting.Contains(ShortName(row.Attributes.GetValueOrDefault("tool_name") ?? row.Name)))
            .OrderBy(row => row.Start)
            .LastOrDefault();
        if (call is null)
        {
            return null;
        }
        var name = ShortName(call.Attributes.GetValueOrDefault("tool_name") ?? call.Name);
        var parts = Describe(call.Input);
        return parts.Length == 0 ? name : $"{name}: {parts}";
    }

    // "done · 3 commits": the call's status, then its commit count when it lists any.
    private static string Describe(string? input)
    {
        if (input is null)
        {
            return string.Empty;
        }
        try
        {
            using var document = JsonDocument.Parse(input);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }
            var status = root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            var commits = root.TryGetProperty("commits", out var c) && c.ValueKind == JsonValueKind.Array ? c.GetArrayLength() : 0;
            var noun = commits == 1 ? "commit" : "commits";
            var commitText = commits == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"{commits} {noun}");
            return string.Join(" · ", new[] { status, commitText }.Where(part => part is not null));
        }
        catch (JsonException)
        {
            // The input is free text from an agent, not ShipLean's own JSON: it says nothing about the outcome.
            return string.Empty;
        }
    }
}
