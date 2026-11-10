using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

// Writes decisions and questions on a work item. The services stamp the agent and publish the change; every tool resolves the
// item first (a person outside the project gets not_found), then parses its arguments, then calls the service.
[McpServerToolType]
internal sealed class AsksTools(ItemResolver itemResolver, MemberResolver members, IDecisionRecorder recorder, IDecisionAnswers answers, IQuestionWrites questions, IItemAsks asks, CaptureSupport support)
{
    [McpServerTool(Name = "record_decisions"), McpScope(McpScopes.Write)]
    [Description("Record the decisions you made on a work item, each with the answer you are going with and the alternatives, for the item's owner to confirm or change. Leave number out to take the next D-number; an open number is edited. A settled or withdrawn number is refused and nothing is written.")]
    public async Task<CallToolResult> RecordDecisions(
        [Description("The item's key, such as SHIP-12.")] string item,
        [Description("The decisions to record, in order.")] IReadOnlyList<DecisionArg> decisions,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var input = decisions ?? [];
        var records = new List<DecisionRecord>();
        for (var i = 0; i < input.Count; i++)
        {
            records.Add(await ToRecordAsync(resolved, input[i], i + 1, cancellationToken));
        }

        var recorded = await recorder.RecordAsync(resolved.Id, records, cancellationToken);
        var current = await asks.GetAsync(resolved.Id, cancellationToken);

        var explicitNumbers = input.Where(d => d?.Number is not null).Select(d => d.Number!.Value).ToHashSet();
        var unnumbered = new Queue<int>(recorded.Created.Where(n => !explicitNumbers.Contains(n)));
        var touched = new List<(int Number, string Status)>();
        var lines = new List<string>();
        foreach (var d in input)
        {
            var number = d?.Number ?? (unnumbered.Count > 0 ? unnumbered.Dequeue() : 0);
            var status = StatusOf(recorded, number);
            touched.Add((number, status));
            var row = current.Decisions.FirstOrDefault(x => x.Number == number);
            if (row is not null)
            {
                lines.Add($"- D{number} ({status}): {row.Question} → {row.CurrentAnswer} · {row.State.ToString().ToLowerInvariant()}");
            }
        }

        var counts = new List<string>();
        AddCount(counts, recorded.Created.Count, "new");
        AddCount(counts, recorded.Updated.Count, "updated");
        AddCount(counts, touched.Count(t => t.Status == "unchanged"), "unchanged");
        var labels = string.Join(", ", touched.Select(t => $"D{t.Number}"));
        var summary = $"Recorded {labels} on {resolved.Key} ({string.Join(", ", counts)})";
        var anchor = touched.Count > 0 ? $"#D{touched[0].Number}" : "";
        var result = new RecordDecisionsResult(
            (await support.ProjectOfAsync(resolved, cancellationToken)).Key, await OpenUrlAsync(resolved, anchor, cancellationToken), resolved.Key, recorded.Created, recorded.Updated);
        return AsksReply.Of(result, summary, string.Join("\n", lines));
    }

    [McpServerTool(Name = "ask"), McpScope(McpScopes.Write)]
    [Description("Ask the item's owner a question you need answered before you can go on. The next agent to pick the item up reads the answer; do not wait for it.")]
    public async Task<CallToolResult> Ask(
        [Description("The item's key, such as SHIP-12.")] string item,
        [Description("The question.")] string text,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var asked = await questions.AskAsync(resolved.Id, text ?? string.Empty, cancellationToken);
        var result = new AskResult(
            (await support.ProjectOfAsync(resolved, cancellationToken)).Key, await OpenUrlAsync(resolved, $"#Q{asked.Number}", cancellationToken), resolved.Key, asked.Number, asked.Label);
        return AsksReply.Of(result, $"Asked {asked.Label} on {resolved.Key}.");
    }

    [McpServerTool(Name = "answer_decision"), McpScope(McpScopes.Write)]
    [Description("Answer a decision on an item as its owner: confirm the recommended answer, change it, or withdraw the decision. For change, answer is recommended, alternative:<n> (1 is the first alternative) or your own words.")]
    public async Task<CallToolResult> AnswerDecision(
        [Description("The item's key, such as SHIP-12.")] string item,
        [Description("The decision's number (3 for D3).")] int number,
        [Description("confirm, change or withdraw.")] string action,
        [Description("For change only: recommended, alternative:<n> (1 is the first alternative), or your own words.")] string? answer = null,
        [Description("Why, shown in the decision's history.")] string? note = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var request = ParseAction(number, action, answer, note);
        await answers.AnswerAsync(resolved.Id, request, cancellationToken);

        var row = (await asks.GetAsync(resolved.Id, cancellationToken)).Decisions.Single(d => d.Number == number);
        var state = row.State.ToString().ToLowerInvariant();
        var result = new AnswerDecisionResult(
            (await support.ProjectOfAsync(resolved, cancellationToken)).Key, await OpenUrlAsync(resolved, $"#D{number}", cancellationToken), resolved.Key, number, state, row.CurrentAnswer);
        return AsksReply.Of(result, $"D{number} on {resolved.Key} is now {state}.");
    }

    [McpServerTool(Name = "answer_question"), McpScope(McpScopes.Write)]
    [Description("Answer a question on an item with a Markdown comment. The comment is posted to Discussion and the question is marked answered.")]
    public async Task<CallToolResult> AnswerQuestion(
        [Description("The item's key, such as SHIP-12.")] string item,
        [Description("The question's number (3 for Q3).")] int number,
        [Description("The answer, in Markdown.")] string answer,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var commentId = await questions.AnswerAsync(resolved.Id, number, answer ?? string.Empty, cancellationToken);
        var result = new AnswerQuestionResult(
            (await support.ProjectOfAsync(resolved, cancellationToken)).Key, await OpenUrlAsync(resolved, $"#Q{number}", cancellationToken), resolved.Key, number, commentId);
        return AsksReply.Of(result, $"Answered Q{number} on {resolved.Key}.");
    }

    private static string StatusOf(RecordedDecisions recorded, int number)
    {
        if (recorded.Created.Contains(number))
        {
            return "new";
        }

        return recorded.Updated.Contains(number) ? "updated" : "unchanged";
    }

    private static void AddCount(List<string> counts, int count, string label)
    {
        if (count > 0)
        {
            counts.Add($"{count} {label}");
        }
    }

    private async Task<DecisionRecord> ToRecordAsync(ResolvedItem resolved, DecisionArg? arg, int position, CancellationToken cancellationToken)
    {
        arg ??= new DecisionArg();
        var prefix = arg.Number is { } n ? $"D{n}: " : $"Decision {position}: ";
        SettledAnswer? settled = null;
        if (arg.Settled is { } s)
        {
            var by = await members.ResolveAsync(resolved.ProjectId, s.By ?? string.Empty, "decisions", cancellationToken);
            if (!DateOnly.TryParseExact(s.On, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var on))
            {
                throw ApiException.Validation($"{prefix}settled.on must be a day as yyyy-MM-dd.", "decisions");
            }

            var (kind, index, text) = ParseAnswer(s.Answer, $"{prefix}settled.answer", "decisions");
            settled = new SettledAnswer(by.Id, on, kind, index, text);
        }

        return new DecisionRecord(arg.Number, arg.Question ?? string.Empty, arg.Recommended ?? string.Empty, arg.Alternatives ?? [], settled);
    }

    private static DecisionAnswer ParseAction(int number, string? action, string? answer, string? note)
    {
        switch ((action ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "confirm":
                RequireNoAnswer(answer, "confirm");
                return new DecisionAnswer(number, DecisionAnswerAction.Confirm, null, null, null, note);
            case "withdraw":
                RequireNoAnswer(answer, "withdraw");
                return new DecisionAnswer(number, DecisionAnswerAction.Withdraw, null, null, null, note);
            case "change":
                var (kind, index, text) = ParseAnswer(answer, "answer", "answer");
                return new DecisionAnswer(number, DecisionAnswerAction.Change, kind, index, text, note);
            default:
                throw ApiException.Validation($"\"{action}\" is not an action: use confirm, change or withdraw.", "action");
        }
    }

    private static void RequireNoAnswer(string? answer, string action)
    {
        if (!string.IsNullOrWhiteSpace(answer))
        {
            throw ApiException.Validation($"A {action} takes no answer: leave it out.", "answer");
        }
    }

    // On the wire alternatives count from 1; in the contracts from 0.
    private static (AnswerKind Kind, int? Index, string? Text) ParseAnswer(string? answer, string label, string field)
    {
        var text = (answer ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            throw ApiException.Validation($"{label} is blank: use recommended, alternative:<n> or your own words.", field);
        }

        if (text.Equals("recommended", StringComparison.OrdinalIgnoreCase))
        {
            return (AnswerKind.Recommended, null, null);
        }

        const string Prefix = "alternative:";
        if (text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(text[Prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
            {
                throw ApiException.Validation($"{label}: \"{text}\" is not an alternative: use alternative:1 for the first.", field);
            }

            return (AnswerKind.Alternative, n - 1, null);
        }

        return (AnswerKind.Own, null, text);
    }

    private async Task<string> OpenUrlAsync(ResolvedItem resolved, string anchor, CancellationToken cancellationToken)
    {
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        return McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key) + anchor;
    }
}
