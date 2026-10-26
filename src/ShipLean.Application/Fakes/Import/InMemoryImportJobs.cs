using System.Globalization;
using System.Text.RegularExpressions;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;
using ShipLean.Core;

namespace ShipLean.Application.Fakes.Import;

// Records the state transitions of import jobs and nothing else: it runs no import and publishes no events. A test arranges
// connections, empty projects and finished jobs through the Register and Arrange methods.
public sealed partial class InMemoryImportJobs(TimeProvider time, ICurrentUser currentUser, IUserDirectory users) : IImportJobs
{
    private readonly object _gate = new();
    private readonly List<InMemoryImportJob> _jobs = [];
    private readonly HashSet<Guid> _connections = [];
    private readonly HashSet<Guid> _emptyProjects = [];
    private readonly HashSet<Guid> _nonEmptyProjects = [];
    private readonly HashSet<string> _takenKeys = [];
    private long _logId;

    public void RegisterConnection(Guid connectionId)
    {
        lock (_gate)
        {
            _connections.Add(connectionId);
        }
    }

    public void RegisterEmptyProject(Guid projectId)
    {
        lock (_gate)
        {
            _emptyProjects.Add(projectId);
        }
    }

    public void RegisterNonEmptyProject(Guid projectId)
    {
        lock (_gate)
        {
            _nonEmptyProjects.Add(projectId);
        }
    }

    public void RegisterProjectKey(string key)
    {
        lock (_gate)
        {
            _takenKeys.Add(key);
        }
    }

    // A job in the given state with that many pending errors; a job that has finished gets a finish time.
    public Guid ArrangeJob(Guid connectionId, ImportJobState status, int pendingErrors, Guid? projectId = null)
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            var job = NewJob(connectionId, "ARRANGED", "ARR", projectId, now, currentUser.Id, NoMapping);
            job.Status = status;
            job.FinishedAt = status is ImportJobState.Queued or ImportJobState.Running ? null : now;
            for (var i = 1; i <= pendingErrors; i++)
            {
                job.Errors.Add(new InMemoryImportError
                {
                    Id = Guid.NewGuid(), Step = ImportStep.Attachments, Entity = "attachment", SourceKey = $"ARRANGED-{i}", Message = "Jira timed out (504)",
                });
            }
            AddLog(job, ImportLogLevel.Info, $"Arranged as {status}");
            return job.Id;
        }
    }

    public async Task<ImportJobDto> StartAsync(ImportStartRequest request, CancellationToken cancellationToken = default)
    {
        var target = request.Target;
        if (string.IsNullOrWhiteSpace(request.JiraProjectKey))
        {
            throw ApiException.Validation("Choose the Jira project to import.");
        }
        if (target.Kind == ImportTargetKind.NewProject && (target.Key is null || !ProjectKey().IsMatch(target.Key)))
        {
            throw ApiException.Validation("A project key is 2 to 4 capital letters.");
        }
        if (target.Kind == ImportTargetKind.NewProject && string.IsNullOrWhiteSpace(target.Name))
        {
            throw ApiException.Validation("Give the new project a name.");
        }
        if (target.Kind == ImportTargetKind.ExistingProject && target.ProjectId is null)
        {
            throw ApiException.Validation("Choose the empty project to import into.");
        }
        var creator = await FindCreatorAsync(cancellationToken);
        lock (_gate)
        {
            if (!_connections.Contains(request.ConnectionId))
            {
                throw ApiException.NotFound("That Jira connection does not exist.");
            }
            if (target.Kind == ImportTargetKind.ExistingProject && _nonEmptyProjects.Contains(target.ProjectId!.Value))
            {
                throw ApiException.Conflict("That project already has work items; choose an empty one.");
            }
            if (target.Kind == ImportTargetKind.ExistingProject && !_emptyProjects.Contains(target.ProjectId!.Value))
            {
                throw ApiException.NotFound("That project does not exist.");
            }
            if (target.Kind == ImportTargetKind.NewProject && _takenKeys.Contains(target.Key!))
            {
                throw ApiException.Conflict($"The key {target.Key} is already taken.");
            }
            if (_jobs.Any(j => j.ConnectionId == request.ConnectionId && j.JiraProjectKey == request.JiraProjectKey && j.Status is ImportJobState.Queued or ImportJobState.Running))
            {
                throw ApiException.Conflict($"{request.JiraProjectKey} is already being imported.");
            }
            var job = NewJob(request.ConnectionId, request.JiraProjectKey, target.Key ?? "EXIS", target.ProjectId, time.GetUtcNow(), creator?.Id, request.Mapping);
            AddLog(job, ImportLogLevel.Info, $"Queued the import of {request.JiraProjectKey}");
            return ToDto(job, creator);
        }
    }

    public async Task<ImportJobDto> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var creators = await CreatorsAsync(cancellationToken);
        lock (_gate)
        {
            return ToDtoWith(Find(jobId), creators);
        }
    }

    public async Task<IReadOnlyList<ImportJobDto>> ListAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        var creators = await CreatorsAsync(cancellationToken);
        lock (_gate)
        {
            return [.. _jobs.Where(j => projectId is null || j.ProjectId == projectId).Reverse().Select(j => ToDtoWith(j, creators))];
        }
    }

    public Task<ImportJobDto> StopAsync(Guid jobId, CancellationToken cancellationToken = default)
        => Change(jobId, job =>
        {
            if (job.Status is not (ImportJobState.Queued or ImportJobState.Running))
            {
                throw ApiException.Conflict("That import already finished.");
            }
            job.Status = ImportJobState.Stopped;
            job.FinishedAt = time.GetUtcNow();
            AddLog(job, ImportLogLevel.Info, "Stopped after the current batch");
        });

    public Task<ImportJobDto> RetryAsync(Guid errorId, CancellationToken cancellationToken = default)
        => Resolve(errorId, ImportErrorResolution.Retried);

    public Task<ImportJobDto> SkipAsync(Guid errorId, CancellationToken cancellationToken = default)
        => Resolve(errorId, ImportErrorResolution.Skipped);

    public Task<ImportJobDto> RetryAllAsync(Guid jobId, CancellationToken cancellationToken = default)
        => Change(jobId, job =>
        {
            var pending = job.Errors.Where(e => e.Resolution == ImportErrorResolution.Pending).ToList();
            if (pending.Count == 0)
            {
                throw ApiException.Conflict("Nothing is waiting to be retried.");
            }
            pending.ForEach(e => e.Resolution = ImportErrorResolution.Retried);
            FinishIfResolved(job);
        });

    public Task<ImportJobDto> RerunAsync(Guid jobId, ImportMappingDto? mapping, CancellationToken cancellationToken = default)
        => Change(jobId, job =>
        {
            if (job.Status is ImportJobState.Queued or ImportJobState.Running)
            {
                throw ApiException.Conflict("That import is still going.");
            }
            job.Status = ImportJobState.Queued;
            job.Mapping = mapping ?? job.Mapping;
            job.StartedAt = time.GetUtcNow();
            job.FinishedAt = null;
            job.Progress = Initial(rerun: true);
            AddLog(job, ImportLogLevel.Info, $"Queued a re-run of {job.JiraProjectKey}");
        });

    public Task<ImportMappingDto> GetMappingAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Find(jobId).Mapping);
        }
    }

    public Task<IReadOnlyList<ImportLogLineDto>> GetLogAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<ImportLogLineDto>>([.. Find(jobId).Log.Select(l => new ImportLogLineDto(
                l.Id, l.LoggedAt, l.LoggedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture), l.Level, l.Message))]);
        }
    }

    public Task<IReadOnlyList<ImportErrorDto>> GetErrorsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var errors = Find(jobId).Errors;
            return Task.FromResult<IReadOnlyList<ImportErrorDto>>([.. errors.Where(e => e.Resolution == ImportErrorResolution.Pending).Concat(errors.Where(e => e.Resolution != ImportErrorResolution.Pending))
                .Select(e => new ImportErrorDto(e.Id, e.Step, e.Entity, e.SourceKey, e.Message, e.Resolution))]);
        }
    }

    private async Task<ImportJobDto> Resolve(Guid errorId, ImportErrorResolution resolution)
    {
        var creators = await CreatorsAsync(CancellationToken.None);
        lock (_gate)
        {
            var job = _jobs.FirstOrDefault(j => j.Errors.Any(e => e.Id == errorId)) ?? throw ApiException.NotFound("That error does not exist.");
            var error = job.Errors.First(e => e.Id == errorId);
            if (error.Resolution != ImportErrorResolution.Pending)
            {
                throw ApiException.Conflict("That error is already resolved.");
            }
            error.Resolution = resolution;
            FinishIfResolved(job);
            return ToDtoWith(job, creators);
        }
    }

    private async Task<ImportJobDto> Change(Guid jobId, Action<InMemoryImportJob> change)
    {
        var creators = await CreatorsAsync(CancellationToken.None);
        lock (_gate)
        {
            var job = Find(jobId);
            change(job);
            return ToDtoWith(job, creators);
        }
    }

    private void FinishIfResolved(InMemoryImportJob job)
    {
        if (job.Status == ImportJobState.DoneWithErrors && job.Errors.All(e => e.Resolution != ImportErrorResolution.Pending))
        {
            job.Status = ImportJobState.Done;
            AddLog(job, ImportLogLevel.Info, "Every error is retried or skipped");
        }
    }

    private InMemoryImportJob Find(Guid jobId) => _jobs.FirstOrDefault(j => j.Id == jobId) ?? throw ApiException.NotFound("That import does not exist.");

    private InMemoryImportJob NewJob(Guid connectionId, string jiraKey, string projectKey, Guid? projectId, DateTimeOffset now, Guid? creator, ImportMappingDto mapping)
    {
        var job = new InMemoryImportJob
        {
            Id = Guid.NewGuid(), ConnectionId = connectionId, JiraProjectKey = jiraKey, ProjectKey = projectKey, ProjectId = projectId,
            Status = ImportJobState.Queued, Progress = Initial(rerun: false), Mapping = mapping, StartedAt = now, CreatedBy = creator,
        };
        _jobs.Add(job);
        return job;
    }

    private void AddLog(InMemoryImportJob job, ImportLogLevel level, string message)
        => job.Log.Add(new InMemoryImportLog(++_logId, time.GetUtcNow(), level, message));

    private static ImportJobProgressJson Initial(bool rerun)
    {
        var none = new ImportEntityCount(0, 0, 0);
        return new ImportJobProgressJson(0, null, "Queued", rerun, new ImportCounts(none, none, none, none, none), 0, 0, 0, null);
    }

    private async Task<UserRef?> FindCreatorAsync(CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(currentUser.Id, cancellationToken);
        return new UserRef(user.Id, user.Name, user.Initials, user.AvatarColour, user.PhotoUrl);
    }

    private async Task<IReadOnlyDictionary<Guid, UserRef>> CreatorsAsync(CancellationToken cancellationToken)
    {
        Guid[] ids;
        lock (_gate)
        {
            ids = [.. _jobs.Where(j => j.CreatedBy is not null).Select(j => j.CreatedBy!.Value).Distinct()];
        }
        var refs = new Dictionary<Guid, UserRef>();
        foreach (var id in ids)
        {
            var user = await users.GetAsync(id, cancellationToken);
            refs[id] = new UserRef(user.Id, user.Name, user.Initials, user.AvatarColour, user.PhotoUrl);
        }
        return refs;
    }

    private ImportJobDto ToDtoWith(InMemoryImportJob job, IReadOnlyDictionary<Guid, UserRef> creators)
        => ToDto(job, job.CreatedBy is { } id && creators.TryGetValue(id, out var creator) ? creator : null);

    private ImportJobDto ToDto(InMemoryImportJob job, UserRef? creator) => new(
        job.Id, job.JiraProjectKey, job.ProjectKey, job.ProjectId, job.Status, job.Progress,
        job.StartedAt, job.StartedAt is { } started ? DisplayDate.Relative(started, time) : null,
        job.FinishedAt, job.FinishedAt is { } finished ? DisplayDate.Relative(finished, time) : null, creator);

    private static ImportMappingDto NoMapping { get; } = new([], [], [], new ImportFieldsDto("", [], true, true, true, true), new ImportTotalsDto(0, 0, 0, 0, 0, 0), []);

    [GeneratedRegex("^[A-Z]{2,4}$")]
    private static partial Regex ProjectKey();
}
