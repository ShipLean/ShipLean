using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.Boards.View;

// The board's filter query parameters, parsed. A card the filter hides stays on the board's counts.
public sealed class BoardFilter
{
    private readonly string? _text;
    private readonly IdMatch? _assignee;
    private readonly IdMatch? _owner;
    private readonly IdMatch? _sprint;
    private readonly IdMatch? _epic;
    private readonly HealthMatch? _health;

    private BoardFilter(BoardViewQuery query)
    {
        _text = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        _assignee = IdMatch.Parse(query.Assignee, "assignee");
        _owner = IdMatch.Parse(query.Owner, "owner");
        _sprint = IdMatch.Parse(Single(query.Sprint), "sprint");
        _epic = IdMatch.Parse(Single(query.Epic), "epic");
        _health = HealthMatch.Parse(query.Health);
    }

    public static BoardFilter From(BoardViewQuery query) => new(query);

    public bool Shows(BoardCardRow card, Guid? epicId)
        => (_text is null || card.Key.Contains(_text, StringComparison.OrdinalIgnoreCase) || card.Title.Contains(_text, StringComparison.OrdinalIgnoreCase))
        && (_assignee?.Matches(card.AssigneeId) ?? true)
        && (_owner?.Matches(card.AssigneeId) ?? true)
        && (_sprint?.Matches(card.SprintId) ?? true)
        && (_epic?.Matches(epicId) ?? true)
        && (_health?.Matches(WorkItemFieldView.ParseHealth(card.Health)) ?? true);

    private static string[] Single(string? value) => value is null ? [] : [value];

    // One health (onTrack, atRisk, blocked, any case) matches a Feature with it; "none" matches a card with no health set.
    private sealed record HealthMatch(FeatureHealth? Health)
    {
        public static HealthMatch? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = value.Trim();
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return new HealthMatch((FeatureHealth?)null);
            }

            // Names only: Enum.TryParse would take "1" as atRisk.
            var name = Enum.GetNames<FeatureHealth>().FirstOrDefault(n => n.Equals(text, StringComparison.OrdinalIgnoreCase));
            return name is null
                ? throw ApiException.Validation("The health filter must be onTrack, atRisk, blocked or none.", "health")
                : new HealthMatch(Enum.Parse<FeatureHealth>(name));
        }

        public bool Matches(FeatureHealth? value) => value == Health;
    }

    // "none" matches a card with no value; an id matches that value; several values match a card with any of them.
    private sealed record IdMatch(HashSet<Guid?> Ids)
    {
        public static IdMatch? Parse(IReadOnlyList<string>? values, string name)
        {
            var given = (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
            return given.Count == 0 ? null : new IdMatch([.. given.Select(v => Value(v, name))]);
        }

        public bool Matches(Guid? value) => Ids.Contains(value);

        private static Guid? Value(string text, string name)
        {
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Guid.TryParse(text, out var id) ? id : throw ApiException.Validation($"The {name} filter must be an id or none.", name);
        }
    }
}
