using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Views;

namespace ShipLean.Application.Features.Boards.View;

public enum LaneMode
{
    // One lane holds every card (lanes off, or a level without lanes).
    Single,

    // The board's stored lanes: criteria lanes first match wins, then the manual lane the card was dragged to, else the default lane.
    Stored,

    // One lane per Epic, then No Epic (the Features board).
    ByEpic,
}

// The lanes a board shows now and where each card sits in them.
public sealed class LaneLayout(LaneMode mode, IReadOnlyList<LaneDef> lanes)
{
    public const string NoEpicLaneId = "lane-no-epic";

    public const string EpicLanePrefix = "epic-";

    public LaneMode Mode => mode;

    public IReadOnlyList<LaneDef> Lanes => lanes;

    public static string EpicLaneId(Guid epicId) => EpicLanePrefix + epicId;

    public LaneDef? Find(string id) => lanes.FirstOrDefault(l => l.Id == id);

    public LaneDef LaneOf(BoardCardRow card, Guid? epicId, ICriteriaEvaluator evaluator)
    {
        switch (mode)
        {
            case LaneMode.Single:
                return lanes[0];
            case LaneMode.ByEpic:
                return (epicId is { } id ? Find(EpicLaneId(id)) : null) ?? Find(NoEpicLaneId)!;
            default:
                var values = new CardFieldValues(card, epicId);
                return lanes.FirstOrDefault(l => HasClauses(l) && evaluator.Matches(l.Criteria!, values))
                    ?? lanes.FirstOrDefault(l => l.Kind == BoardLaneKind.Manual && l.Id == card.LaneId)
                    ?? lanes.First(l => l.Kind == BoardLaneKind.Default);
        }
    }

    // The criteria lanes the card matches now (all of them, not only the first): the fields a manual or default drop must reset.
    public IEnumerable<LaneDef> MatchedCriteriaLanes(BoardCardRow card, Guid? epicId, ICriteriaEvaluator evaluator)
    {
        var values = new CardFieldValues(card, epicId);
        return lanes.Where(l => HasClauses(l) && evaluator.Matches(l.Criteria!, values));
    }

    // A criteria lane with no clauses matches no card (the evaluator refuses empty criteria) and takes no drops.
    private static bool HasClauses(LaneDef lane) => lane.Kind == BoardLaneKind.Criteria && lane.Criteria is { Clauses.Count: > 0 };
}
