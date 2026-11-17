using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.InspectionMetrics.Periods;

// Who did a task, the rule lead time and cost share (CK3). The sprint a task's lead time ended in is InspectionPeriods.SprintOf of
// its end, for every started sprint, shown or not.
public static class TaskPlacement
{
    public const string PeopleLabel = "People";

    // The user who first moved each task to an In-progress state, by task key (null user ids are kept as null).
    public static IReadOnlyDictionary<string, Guid?> Starters(LeadTimeFacts facts)
        => facts.Moves
            .Where(move => move.Category == StateCategory.InProgress)
            .GroupBy(move => move.ItemKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(move => move.At).ThenBy(move => move.Rev).First().UserId, StringComparer.Ordinal);

    // Who did the task: the agent connection that started it, else the person who started it, else its assignee.
    public static string Who(LeadTimeTaskFact fact, Guid? starter, IReadOnlyDictionary<Guid, string> people)
    {
        if (fact.AgentName is not null)
        {
            return fact.AgentName;
        }
        if (starter is { } id && people.TryGetValue(id, out var name))
        {
            return name;
        }
        return fact.AssigneeName ?? PeopleLabel;
    }
}
