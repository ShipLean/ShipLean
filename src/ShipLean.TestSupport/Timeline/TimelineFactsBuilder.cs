using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.TestSupport.Timeline;

// The test double of C3: a fluent builder of TimelineFacts for the timeline builder's tests. Defaults give a valid Feature
// (SHIP-99 "Coupons v2", New, created at Start, no epic) in project SHIP with nothing under it. A story, bug or task whose
// current state isn't given takes the state of its latest move, or New when it has none. A move or Blocked tag change whose
// revision isn't given takes the item's next revision, counted per item from 2 (revision 1 is the creation).
public sealed class TimelineFactsBuilder
{
    // Monday 5 October 2026, 00:00 UTC: the mock's first day.
    public static readonly DateTimeOffset Start = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    public static readonly Guid FeatureId = new("00000000-0000-4000-8000-000000001465");

    private readonly List<(string Key, WorkItemTypeId Type, string Title, Guid? Assignee, DateTimeOffset CreatedAt, string? State, StateCategory? Category)> _stories = [];
    private readonly List<(string Key, string Parent, string Title, Guid? Assignee, string? State, StateCategory? Category)> _tasks = [];
    private readonly List<TimelineMoveFact> _moves = [];
    private readonly List<TimelineBlockedTagFact> _blockedTags = [];
    private readonly List<TimelineLinkFact> _links = [];
    private readonly Dictionary<string, string> _waves = new(StringComparer.Ordinal);
    private readonly List<TimelineReviewFact> _reviews = [];
    private readonly List<TimelineSprintFact> _sprints = [];
    private readonly Dictionary<Guid, string> _people = [];
    private readonly Dictionary<string, int> _revs = new(StringComparer.Ordinal);
    private string _projectKey = "SHIP";
    private TimelineFeatureFact _feature = new(FeatureId, "SHIP-99", "Coupons v2", "New", StateCategory.Proposed, Start, null, null);
    private DateTimeOffset? _planAppliedAt;

    public TimelineFactsBuilder WithProjectKey(string projectKey)
    {
        _projectKey = projectKey;
        return this;
    }

    public TimelineFactsBuilder WithFeature(
        string key, string title = "Coupons v2", string stateName = "New", StateCategory category = StateCategory.Proposed,
        DateTimeOffset? createdAt = null, Guid? createdBy = null)
    {
        _feature = _feature with { Key = key, Title = title, StateName = stateName, StateCategory = category, CreatedAt = createdAt ?? Start, CreatedBy = createdBy };
        return this;
    }

    public TimelineFactsBuilder WithFeatureState(string stateName, StateCategory category)
    {
        _feature = _feature with { StateName = stateName, StateCategory = category };
        return this;
    }

    public TimelineFactsBuilder WithEpic(string key, string title)
    {
        _feature = _feature with { Epic = new TimelineEpicFact(key, title) };
        return this;
    }

    public TimelineFactsBuilder WithPlanApplied(DateTimeOffset at)
    {
        _planAppliedAt = at;
        return this;
    }

    public TimelineFactsBuilder WithStory(
        string key, string title = "Story", Guid? assignee = null, DateTimeOffset? createdAt = null, string? stateName = null, StateCategory? category = null)
        => WithParent(key, WorkItemTypeId.Story, title, assignee, createdAt, stateName, category);

    public TimelineFactsBuilder WithBug(
        string key, string title = "Bug", Guid? assignee = null, DateTimeOffset? createdAt = null, string? stateName = null, StateCategory? category = null)
        => WithParent(key, WorkItemTypeId.Bug, title, assignee, createdAt, stateName, category);

    public TimelineFactsBuilder WithTask(
        string key, string parentKey, string title = "Task", Guid? assignee = null, string? stateName = null, StateCategory? category = null)
    {
        _tasks.Add((key, parentKey, title, assignee, stateName, category));
        return this;
    }

    // A move of the item (the Feature, a story, bug or task) to a state at a time, by a user, through an agent when agentName is set.
    public TimelineFactsBuilder WithMove(
        string itemKey, DateTimeOffset at, string stateName, StateCategory category, string? agentName = null, Guid? userId = null, int? rev = null)
    {
        _moves.Add(new TimelineMoveFact(itemKey, at, rev ?? NextRev(itemKey), stateName, category, agentName, userId));
        return this;
    }

    // The task's Blocked tag added (blockedAfter true) or removed (false) at a time.
    public TimelineFactsBuilder WithBlockedTag(string itemKey, DateTimeOffset at, bool blockedAfter, int? rev = null)
    {
        _blockedTags.Add(new TimelineBlockedTagFact(itemKey, at, rev ?? NextRev(itemKey), blockedAfter));
        return this;
    }

    // A link, already normalised: from must finish before to.
    public TimelineFactsBuilder WithLink(string from, string to, TimelineLinkKind kind = TimelineLinkKind.Predecessor)
    {
        _links.Add(new TimelineLinkFact(from, to, kind));
        return this;
    }

    public TimelineFactsBuilder WithWave(string storyKey, string label)
    {
        _waves[storyKey] = label;
        return this;
    }

    public TimelineFactsBuilder WithReview(
        string taskKey, DateTimeOffset at, ReviewVerdict verdict, Guid byUserId, string? agentName = null, string? note = null)
    {
        _reviews.Add(new TimelineReviewFact(taskKey, at, verdict, note, byUserId, agentName));
        return this;
    }

    public TimelineFactsBuilder WithSprint(string name, DateOnly startDate, DateOnly endDate)
    {
        _sprints.Add(new TimelineSprintFact(name, startDate, endDate));
        return this;
    }

    public TimelineFactsBuilder WithPerson(Guid userId, string name)
    {
        _people[userId] = name;
        return this;
    }

    public TimelineFacts Build()
        => new(
            _projectKey,
            _feature,
            _planAppliedAt,
            [.. _stories.Select(s =>
            {
                var (state, category) = Current(s.Key, s.State, s.Category);
                return new TimelineStoryFact(s.Key, s.Type, s.Title, s.Assignee, s.CreatedAt, state, category);
            })],
            [.. _tasks.Select(t =>
            {
                var (state, category) = Current(t.Key, t.State, t.Category);
                return new TimelineTaskFact(t.Key, t.Parent, t.Title, t.Assignee, state, category);
            })],
            [.. _moves],
            [.. _blockedTags],
            [.. _links],
            new Dictionary<string, string>(_waves, StringComparer.Ordinal),
            [.. _reviews],
            [.. _sprints],
            new Dictionary<Guid, string>(_people));

    private TimelineFactsBuilder WithParent(
        string key, WorkItemTypeId type, string title, Guid? assignee, DateTimeOffset? createdAt, string? stateName, StateCategory? category)
    {
        _stories.Add((key, type, title, assignee, createdAt ?? Start, stateName, category));
        return this;
    }

    private int NextRev(string itemKey)
    {
        var rev = _revs.GetValueOrDefault(itemKey, 1) + 1;
        _revs[itemKey] = rev;
        return rev;
    }

    private (string State, StateCategory Category) Current(string key, string? state, StateCategory? category)
    {
        if (state is not null && category is { } given)
        {
            return (state, given);
        }

        var latest = _moves.Where(m => m.ItemKey == key).OrderBy(m => m.At).ThenBy(m => m.Rev).LastOrDefault();
        return latest is null ? ("New", StateCategory.Proposed) : (latest.StateName, latest.Category);
    }
}
