using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Mcp.Sprint;

// plan_sprint's cut line: walk the unplanned stack top-down and take items while they fit the room that is left after
// what the sprint already holds. The first item that does not fit ends the cut (stack order is never skipped), except
// that an item bigger than the room still lands in a sprint that is otherwise empty (as the forecast does). Over
// capacity is a warning for the caller to show, never a refusal. Unestimated items count zero points.
// Where it still differs from SprintMetrics.GetForecastAsync (S3b-1): the forecast counts the points of Removed stories
// and bugs already in a sprint as planned, while this sees only what IWorkTreeReads.StackAsync returns (never Removed),
// so a sprint holding Removed items has more room here. The forecast also fills only sprints that start after today.
public static class SprintFill
{
    // One apply moves all or nothing in one transaction, so a plan stays small (the same cap as capture_work).
    public const int MaxItems = 25;

    public static FillResult Cut(IReadOnlyList<StackRowDto> stack, decimal room, decimal alreadyIn)
    {
        var used = alreadyIn;
        var selected = new List<StackRowDto>();
        var capped = false;
        foreach (var row in stack)
        {
            if (selected.Count == MaxItems)
            {
                capped = true;
                break;
            }

            var points = row.Points ?? 0m;
            if (used + points <= room || (selected.Count == 0 && alreadyIn == 0))
            {
                selected.Add(row);
                used += points;
                continue;
            }
            break;
        }

        return new FillResult(selected, [.. stack.Skip(selected.Count)], used, used > room ? used - room : null, capped);
    }
}
