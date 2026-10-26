using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.WorkItems;

// What a Feature's own fields must satisfy. The Postgres store and the in-memory fake both call it, so they refuse the same writes.
public static class FeatureFieldRules
{
    public const int GoalMaxLength = 1000;
    public const int StatusLineMaxLength = 500;

    // Goal, status line and the verified stamp belong to a Feature; the status is one line.
    public static void Check(WorkItemTypeId type, string goal, string statusLine, DateTimeOffset? statusVerifiedAt)
    {
        if (type != WorkItemTypeId.Feature)
        {
            if (goal.Length > 0)
            {
                throw ApiException.Validation("Only a Feature has a goal.", "goal");
            }

            if (statusLine.Length > 0 || statusVerifiedAt is not null)
            {
                throw ApiException.Validation("Only a Feature has a status.", "statusLine");
            }

            return;
        }

        if (goal.Length > GoalMaxLength)
        {
            throw ApiException.Validation($"The goal can be at most {GoalMaxLength:N0} characters.", "goal");
        }

        if (statusLine.Length > StatusLineMaxLength || statusLine.AsSpan().ContainsAny('\r', '\n'))
        {
            throw ApiException.Validation($"The status is one line of at most {StatusLineMaxLength:N0} characters.", "statusLine");
        }
    }

    // The slug and start day a create asks for. Uniqueness is the store's: it needs the project's other Features.
    public static void CheckNew(WorkItemTypeId type, string? slug, DateOnly? startedOn)
    {
        if (type != WorkItemTypeId.Feature)
        {
            if (slug is not null)
            {
                throw ApiException.Validation("Only a Feature has a slug.", "slug");
            }

            if (startedOn is not null)
            {
                throw ApiException.Validation("Only a Feature has a start day.", "startedOn");
            }

            return;
        }

        if (slug is not null && !Slugs.IsValid(slug))
        {
            throw ApiException.Validation($"A slug is lower-case letters and digits joined by single hyphens, at most {Slugs.MaxLength} characters.", "slug");
        }
    }

    public static ApiException SlugTaken() => ApiException.SlugTaken("That slug is already used by another Feature in this project.");
}
