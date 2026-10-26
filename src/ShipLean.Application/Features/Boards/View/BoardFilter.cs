using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Boards.View;

// The board's filter query parameters, parsed. A card the filter hides stays on the board's counts.
public sealed class BoardFilter
{
    private readonly string? _text;
    private readonly IdMatch? _assignee;
    private readonly IdMatch? _owner;
    private readonly IdMatch? _sprint;
    private readonly IdMatch? _epic;

    private BoardFilter(BoardViewQuery query)
    {
        _text = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        _assignee = IdMatch.Parse(query.Assignee, "assignee");
        _owner = IdMatch.Parse(query.Owner, "owner");
        _sprint = IdMatch.Parse(Single(query.Sprint), "sprint");
        _epic = IdMatch.Parse(Single(query.Epic), "epic");
    }

    public static BoardFilter From(BoardViewQuery query) => new(query);

    public bool Shows(BoardCardRow card, Guid? epicId)
        => (_text is null || card.Key.Contains(_text, StringComparison.OrdinalIgnoreCase) || card.Title.Contains(_text, StringComparison.OrdinalIgnoreCase))
        && (_assignee?.Matches(card.AssigneeId) ?? true)
        && (_owner?.Matches(card.AssigneeId) ?? true)
        && (_sprint?.Matches(card.SprintId) ?? true)
        && (_epic?.Matches(epicId) ?? true);

    private static string[] Single(string? value) => value is null ? [] : [value];

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
