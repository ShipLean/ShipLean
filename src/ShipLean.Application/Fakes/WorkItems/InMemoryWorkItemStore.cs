using System.Text.Json;
using System.Collections.Concurrent;
using ShipLean.Application.Markdown;
using ShipLean.Application.WorkItems;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Fakes.WorkItems;

// The work item store in memory: thread-safe, no I/O, publishes no events. Every write runs on a copy of the data that
// replaces the real data only when the whole write (rules, the InTransaction hook) succeeded, which is its transaction.
// Arrange it with AddProject, AddUser and AddSprint before use. Board and backlog order are sortable strings computed before the rules run; the rules may amend them.
public sealed partial class InMemoryWorkItemStore(IWorkItemRules rules, ICurrentUser currentUser, MarkdownRenderer renderer, TimeProvider time, ICurrentAgent currentAgent) : IWorkItemStore, IWorkItemTreeWriter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, UserRef> _users = new();
    private readonly ConcurrentDictionary<Guid, SprintRef> _sprints = new();
    private InMemoryWorld _world = new();

    public void AddUser(UserRef user) => _users[user.Id] = user;

    public void AddSprint(SprintRef sprint) => _sprints[sprint.Id] = sprint;

    public void AddProject(Guid projectId, string key, int nextNumber, IReadOnlyList<StateSnapshot> states)
    {
        _gate.Wait();
        try
        {
            _world.Projects[projectId] = new InMemoryProject(key, nextNumber, states);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<WriteOutcome> CreateAsync(WorkItemCreate create, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, world => CreateInAsync(world, create, options, true, null, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<WriteOutcome>> CreateManyAsync(IReadOnlyList<WorkItemCreate> items, WriteOptions options, CancellationToken cancellationToken = default)
    {
        if (items.Select(i => i.ProjectId).Distinct().Count() > 1)
        {
            throw ApiException.Validation("Work items created together must all be in one project.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var world = _world.Clone();
            var outcomes = new List<WriteOutcome>();
            // The batch lands at the top in input order: the first goes first, the rest in front of what was on top before the batch.
            var top = items.Count == 0
                ? null
                : world.Items.Values.Where(i => i.ProjectId == items[0].ProjectId).OrderBy(i => i.BacklogOrder, StringComparer.Ordinal).FirstOrDefault()?.Id;
            foreach (var create in items)
            {
                outcomes.Add(await CreateInAsync(world, create, options, outcomes.Count == 0, top, cancellationToken));
            }

            await RunHookAsync(options, cancellationToken);
            _world = world;
            return outcomes;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<WriteOutcome> CreateInAsync(
        InMemoryWorld world, WorkItemCreate create, WriteOptions options, bool atTop, Guid? beforeItemId, CancellationToken cancellationToken)
    {
        var project = world.FindProject(create.ProjectId) ?? throw ApiException.NotFound("Project not found.");
        FeatureFieldRules.CheckNew(create.Type, create.Slug, create.StartedOn);
        if (create.Slug is { } slug && world.Items.Values.Any(i => i.ProjectId == create.ProjectId && string.Equals(i.Slug, slug, StringComparison.OrdinalIgnoreCase)))
        {
            throw FeatureFieldRules.SlugTaken();
        }

        var state = project.States.FirstOrDefault(s => s.Type == create.Type && s.Category == StateCategory.Proposed)
            ?? throw ApiException.Validation($"A {create.Type} has no Proposed state.");
        var number = project.NextNumber++;
        var stamp = Stamp(options);
        var seed = NewSnapshot(project, create.ProjectId, number, create.Type, state.Id, stamp) with
        {
            Slug = create.Slug,
            StartedOn = create.Type == WorkItemTypeId.Feature ? create.StartedOn ?? DateOnly.FromDateTime(stamp.At.UtcDateTime) : null,
        };
        var proposed = InMemoryChanges.From(seed) with
        {
            Title = create.Title,
            Goal = create.Goal,
            StatusLine = create.StatusLine,
            StatusVerifiedAt = create.StatusLine.Length > 0 ? stamp.At : null,
            ParentId = create.ParentId,
            SprintId = create.SprintId,
            BacklogOrder = InMemoryOrder.BacklogKey(world, create.ProjectId, seed.Id, beforeItemId, atTop),
        };
        proposed = InMemoryChanges.Apply(proposed, create.Changes, stamp.At);
        var links = InMemoryLinks.Apply(world, seed.Id, [], create.Changes);
        var result = await StageAsync(world, null, seed, proposed, links, true, stamp, null, cancellationToken);
        return new WriteOutcome(result, null);
    }

    public Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, async world =>
        {
            var before = Load(world, id);
            if (before.Revision != patch.ExpectedRevision)
            {
                return StaleOutcome(world, before, patch.ExpectedRevision);
            }

            return new WriteOutcome(await PatchOneAsync(world, before, patch.Changes, null, Stamp(options), cancellationToken), null);
        }, cancellationToken);

    public async Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var world = _world.Clone();
            var stale = patches
                .Select(p => (Item: Load(world, p.Id), p.Patch))
                .Where(p => p.Item.Revision != p.Patch.ExpectedRevision)
                .Select(p => BuilderFor(world).Conflict(p.Item, p.Patch.ExpectedRevision))
                .ToList();
            if (stale.Count > 0)
            {
                return new WriteManyOutcome(null, stale);
            }

            var written = new List<WriteResult>();
            foreach (var (id, patch) in patches)
            {
                written.Add(await PatchOneAsync(world, Load(world, id), patch.Changes, null, Stamp(options), cancellationToken));
            }

            await RunHookAsync(options, cancellationToken);
            _world = world;
            return new WriteManyOutcome(written, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<WriteOutcome> MoveAsync(Guid id, MoveTarget target, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, async world =>
        {
            var before = Load(world, id);
            if (before.Revision != target.ExpectedRevision)
            {
                return StaleOutcome(world, before, target.ExpectedRevision);
            }

            var placement = new InMemoryPlacement(target.ColumnId, target.Done, target.LaneId, target.BeforeItemId);
            return new WriteOutcome(await PatchOneAsync(world, before, target.Set, placement, Stamp(options), cancellationToken), null);
        }, cancellationToken);

    public Task<WriteOutcome> ReorderAsync(Guid id, int expectedRevision, Guid? beforeItemId, CancellationToken cancellationToken = default)
        => WriteAsync(WriteOptions.User, world =>
        {
            var item = Load(world, id);
            if (item.Revision != expectedRevision)
            {
                return Task.FromResult(StaleOutcome(world, item, expectedRevision));
            }

            world.Items[item.Id] = item with { BacklogOrder = InMemoryOrder.BacklogKey(world, item.ProjectId, item.Id, beforeItemId, false) };
            return Task.FromResult(new WriteOutcome(new WriteResult(item.Id, item.Key, item.Revision, [], []), null));
        }, cancellationToken);

    public Task<WriteOutcome> RemoveAsync(Guid id, int expectedRevision, CancellationToken cancellationToken = default)
        => WriteAsync(WriteOptions.User, async world =>
        {
            var before = Load(world, id);
            if (before.Revision != expectedRevision)
            {
                return StaleOutcome(world, before, expectedRevision);
            }

            var removed = world.Projects[before.ProjectId].States.FirstOrDefault(s => s.Type == before.Type && s.Category == StateCategory.Removed)
                ?? throw ApiException.Validation($"A {before.Type} has no Removed state.");
            var proposed = InMemoryChanges.From(before) with { StateId = removed.Id };
            var result = await StageAsync(world, before, before, proposed, before.Links, false, Stamp(WriteOptions.User), null, cancellationToken);
            return new WriteOutcome(result, null);
        }, cancellationToken);

    public async Task<ImportResult> ImportAsync(Guid projectId, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var world = _world.Clone();
            var project = world.FindProject(projectId) ?? throw ApiException.NotFound("Project not found.");
            var existing = world.Items.Values.FirstOrDefault(i =>
                i.ProjectId == projectId && i.OriginalSystem?.System == item.OriginalSystem.System && i.OriginalSystem.Id == item.OriginalSystem.Id);
            var result = existing is null
                ? ImportNew(world, project, projectId, importJobId, item)
                : await ImportExistingAsync(world, existing, importJobId, item, cancellationToken);
            _world = world;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkItemSnapshot> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return Load(_world, id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RevisionSnapshot>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Load(_world, id);
            return [.. _world.Revisions[id]];
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<WriteOutcome> WriteAsync(WriteOptions options, Func<InMemoryWorld, Task<WriteOutcome>> body, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var world = _world.Clone();
            var outcome = await body(world);
            if (outcome.Written is null)
            {
                return outcome;
            }

            await RunHookAsync(options, cancellationToken);
            _world = world;
            return outcome;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task RunHookAsync(WriteOptions options, CancellationToken cancellationToken)
    {
        if (options.InTransaction is { } hook)
        {
            using var transaction = new InMemoryTransaction();
            await hook(transaction, cancellationToken);
        }
    }

    private InMemoryStamp Stamp(WriteOptions options)
        => new(currentUser.Id, time.GetUtcNow(), options.Origin == RevisionOrigin.Jira ? "jira" : null, null, AgentStamp.Of(currentAgent), options.Via);

    private static WorkItemSnapshot Load(InMemoryWorld world, Guid id)
        => world.FindItem(id) ?? throw ApiException.NotFound("Work item not found.");

    private WriteOutcome StaleOutcome(InMemoryWorld world, WorkItemSnapshot current, int expectedRevision)
        => new(null, BuilderFor(world).Conflict(current, expectedRevision));

    private InMemoryDtoBuilder BuilderFor(InMemoryWorld world) => new(world, _users, _sprints, renderer, time);

    private static WorkItemSnapshot NewSnapshot(InMemoryProject project, Guid projectId, int number, WorkItemTypeId type, Guid stateId, InMemoryStamp stamp) => new(
        Guid.NewGuid(), projectId, number, $"{project.Key}-{number}", type, "", "", [], "", null, stateId, null, null, 2, null, "", null, null, [],
        null, false, null, null, null, 0, stamp.By, stamp.At, stamp.By, stamp.At, stamp.At, []);

    private async Task<WriteResult> PatchOneAsync(
        InMemoryWorld world, WorkItemSnapshot before, WorkItemChanges? changes, InMemoryPlacement? placement, InMemoryStamp stamp, CancellationToken cancellationToken)
    {
        var proposed = InMemoryChanges.Apply(InMemoryChanges.From(before), changes, stamp.At);
        if (placement is not null)
        {
            proposed = proposed with { ColumnId = placement.ColumnId, ColumnDone = placement.Done, LaneId = placement.LaneId ?? before.LaneId };
            proposed = proposed with { ColumnOrder = InMemoryOrder.ColumnKey(world, proposed, placement.BeforeItemId) };
        }

        var links = InMemoryLinks.Apply(world, before.Id, before.Links, changes);
        return await StageAsync(world, before, before, proposed, links, true, stamp, null, cancellationToken);
    }

    // Runs the rules, validates, diffs against the stored item, and writes the item (and a revision when a field changed).
    private async Task<WriteResult> StageAsync(
        InMemoryWorld world,
        WorkItemSnapshot? before,
        WorkItemSnapshot baseline,
        ProposedChange proposed,
        IReadOnlyList<LinkSnapshot> links,
        bool runRules,
        InMemoryStamp stamp,
        IReadOnlyDictionary<string, JsonElement>? appendedTo,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> notices = [];
        if (runRules)
        {
            var outcome = await rules.ApplyAsync(before, proposed, currentUser.Id, null, cancellationToken);
            var amended = outcome.AmendedChange;
            if (amended.ColumnId != proposed.ColumnId && amended.ColumnOrder == proposed.ColumnOrder)
            {
                amended = amended with { ColumnOrder = amended.ColumnId is null ? null : InMemoryOrder.ColumnKey(world, amended, null) };
            }

            proposed = amended;
            notices = outcome.Notices;
        }

        InMemoryChanges.Validate(proposed, world, _users, _sprints);
        var labels = LabelsFor(world);
        var beforeView = before is null ? WorkItemFieldView.Blank : ViewOf(InMemoryChanges.From(before), before.Links);
        var changes = WorkItemFieldDiff.Between(beforeView, ViewOf(proposed, links), labels)
            .Where(c => appendedTo is null || !(appendedTo.TryGetValue(c.Field, out var to) && JsonElement.DeepEquals(to, c.To)))
            .ToList();
        var written = changes.Count > 0;
        var revision = written ? baseline.Revision + 1 : baseline.Revision;
        var snapshot = Materialise(world, baseline, proposed, links, stamp.At) with
        {
            Revision = revision,
            ChangedBy = written ? stamp.By : baseline.ChangedBy,
            ChangedAt = written ? stamp.At : baseline.ChangedAt,
        };
        if (proposed.StatusVerifiedAt != baseline.StatusVerifiedAt)
        {
            snapshot = snapshot with { StatusVerifiedBy = stamp.By, StatusVerifiedAgent = stamp.AgentName, StatusVerifiedVia = stamp.Via };
        }

        world.Items[snapshot.Id] = snapshot;
        if (written)
        {
            AddRevision(world, snapshot.Id, new RevisionSnapshot(revision, stamp.By, stamp.At, stamp.OriginalSystem, stamp.ImportJobId, changes, stamp.AgentName));
        }

        return new WriteResult(snapshot.Id, snapshot.Key, revision, changes, notices);
    }

    private static void AddRevision(InMemoryWorld world, Guid id, RevisionSnapshot revision)
    {
        if (!world.Revisions.TryGetValue(id, out var list))
        {
            list = [];
            world.Revisions[id] = list;
        }

        list.Add(revision);
    }

    private static WorkItemSnapshot Materialise(InMemoryWorld world, WorkItemSnapshot baseline, ProposedChange p, IReadOnlyList<LinkSnapshot> links, DateTimeOffset at) => baseline with
    {
        StateChangedAt = p.StateId == baseline.StateId ? baseline.StateChangedAt : at,
        Title = p.Title,
        Description = p.Description,
        AcceptanceCriteria = p.AcceptanceCriteria,
        ReproSteps = p.ReproSteps,
        Severity = p.Severity,
        StateId = p.StateId,
        AssigneeId = p.AssigneeId,
        SprintId = p.SprintId,
        Priority = p.Priority,
        Points = p.Points,
        ParentId = p.ParentId,
        ParentType = p.ParentId is { } parent ? world.Items[parent].Type : null,
        Tags = p.Tags,
        Goal = p.Goal,
        StatusLine = p.StatusLine,
        StatusVerifiedAt = p.StatusVerifiedAt,
        ColumnId = p.ColumnId,
        ColumnDone = p.ColumnDone,
        LaneId = p.LaneId,
        ColumnOrder = p.ColumnOrder,
        BacklogOrder = p.BacklogOrder,
        Links = links,
    };

    private static WorkItemFieldView ViewOf(ProposedChange p, IReadOnlyList<LinkSnapshot> links) => new(
        p.Title, p.Type, p.StateId, p.AssigneeId, p.Priority, p.Points, p.Tags, p.SprintId, p.ParentId, p.Severity,
        WorkItemFieldView.NullIfEmpty(p.Description), p.AcceptanceCriteria, WorkItemFieldView.NullIfEmpty(p.ReproSteps), [.. links.Select(l => l.Value)],
        WorkItemFieldView.NullIfEmpty(p.Goal), WorkItemFieldView.NullIfEmpty(p.StatusLine), WorkItemFieldView.Instant(p.StatusVerifiedAt));

    private WorkItemFieldLabels LabelsFor(InMemoryWorld world) => new(
        id => world.Projects.Values.SelectMany(p => p.States).FirstOrDefault(s => s.Id == id)?.Name,
        id => _users.GetValueOrDefault(id)?.Name,
        id => _sprints.GetValueOrDefault(id)?.Name,
        id => world.FindItem(id)?.Title,
        DisplayDate.Format);

    private ImportResult ImportNew(InMemoryWorld world, InMemoryProject project, Guid projectId, Guid importJobId, ImportedWorkItem item)
    {
        if (world.Items.Values.Any(i => i.ProjectId == projectId && i.Number == item.KeyNumber))
        {
            throw ApiException.Conflict($"Key number {item.KeyNumber} belongs to another work item.");
        }

        project.NextNumber = Math.Max(project.NextNumber, item.KeyNumber + 1);
        var seed = NewSnapshot(project, projectId, item.KeyNumber, item.Type, item.StateId, new InMemoryStamp(item.CreatedBy, item.CreatedAt, item.OriginalSystem.System, importJobId)) with
        {
            OriginalSystem = item.OriginalSystem,
        };
        var proposed = ImportedChange(seed, item);
        InMemoryChanges.Validate(proposed, world, _users, _sprints);
        var links = InMemoryLinks.Apply(world, seed.Id, [], new WorkItemChanges { AddLinks = item.Links });
        var revisions = item.Revisions.Count > 0
            ? item.Revisions.Select((r, i) => new RevisionSnapshot(i + 1, r.ChangedBy, r.ChangedAt, item.OriginalSystem.System, importJobId, r.Changes)).ToList()
            : [new RevisionSnapshot(1, item.CreatedBy, item.CreatedAt, item.OriginalSystem.System, importJobId,
                WorkItemFieldDiff.Between(WorkItemFieldView.Blank, ViewOf(proposed, links), LabelsFor(world)))];
        var snapshot = Materialise(world, seed, proposed, links, item.StateChangedAt) with
        {
            Revision = revisions.Count,
            ChangedBy = item.ChangedBy,
            ChangedAt = item.ChangedAt,
            StateChangedAt = item.StateChangedAt,
            BacklogOrder = InMemoryOrder.BacklogKey(world, projectId, seed.Id, null, false),
        };
        world.Items[snapshot.Id] = snapshot;
        world.Revisions[snapshot.Id] = revisions;
        return new ImportResult(snapshot.Id, snapshot.Key, snapshot.Revision, true);
    }

    private async Task<ImportResult> ImportExistingAsync(InMemoryWorld world, WorkItemSnapshot existing, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken)
    {
        if (existing.Type != item.Type)
        {
            throw ApiException.Validation("An imported work item can't change type on a re-run.");
        }

        var proposed = ImportedChange(existing, item);
        var links = InMemoryLinks.Apply(world, existing.Id, existing.Links, new WorkItemChanges { AddLinks = item.Links });
        var stored = world.Revisions[existing.Id];
        var lastImported = stored.Where(r => r.OriginalSystem is not null).Select(r => (DateTimeOffset?)r.ChangedAt).Max();
        var appended = item.Revisions.Where(r => lastImported is null || r.ChangedAt > lastImported).OrderBy(r => r.ChangedAt).ToList();
        var baseline = existing;
        foreach (var revision in appended)
        {
            baseline = baseline with { Revision = baseline.Revision + 1, ChangedBy = revision.ChangedBy, ChangedAt = revision.ChangedAt };
            AddRevision(world, existing.Id, new RevisionSnapshot(baseline.Revision, revision.ChangedBy, revision.ChangedAt, item.OriginalSystem.System, importJobId, revision.Changes));
        }

        // A field the appended Jira history already brought to its final value needs no combined revision; one it left
        // elsewhere does, so history always ends at the item's value.
        var covered = new Dictionary<string, JsonElement>();
        foreach (var change in appended.SelectMany(r => r.Changes))
        {
            covered[change.Field] = change.To;
        }
        var stamp = new InMemoryStamp(item.ChangedBy, item.ChangedAt, item.OriginalSystem.System, importJobId);
        var result = await StageAsync(world, existing, baseline, proposed, links, false, stamp, covered, cancellationToken);
        world.Items[existing.Id] = world.Items[existing.Id] with { StateChangedAt = item.StateChangedAt };
        return new ImportResult(result.Id, result.Key, result.Revision, false);
    }

    private static ProposedChange ImportedChange(WorkItemSnapshot baseline, ImportedWorkItem item) => InMemoryChanges.From(baseline) with
    {
        Title = item.Title,
        Description = item.Description,
        AcceptanceCriteria = item.AcceptanceCriteria,
        ReproSteps = item.ReproSteps,
        Severity = item.Severity,
        StateId = item.StateId,
        AssigneeId = item.AssigneeId,
        SprintId = item.SprintId,
        Priority = item.Priority,
        Points = WorkItemPoints.Normalise(item.Points),
        ParentId = item.ParentId,
        Tags = item.Tags,
    };
}
