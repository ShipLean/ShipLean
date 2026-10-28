using System.Text;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.LinearApi;

internal static class LinearDescription
{
    // The description with the acceptance criteria appended as a Markdown checklist, exactly as the webhook's issue has it
    // (AgentDescription in AgentDispatch; features reach each other only through Contracts, so this is a copy). Null when
    // there is neither.
    public static string? WithCriteria(string? description, IReadOnlyList<ChecklistItem> criteria)
    {
        var text = string.IsNullOrWhiteSpace(description) ? null : description.TrimEnd();
        if (criteria.Count == 0)
        {
            return text;
        }
        var builder = new StringBuilder(text ?? string.Empty);
        if (text != null)
        {
            builder.Append("\n\n");
        }
        builder.Append("## Acceptance criteria");
        foreach (var criterion in criteria)
        {
            builder.Append(criterion.Done ? "\n- [x] " : "\n- [ ] ").Append(criterion.Text);
        }
        return builder.ToString();
    }
}
