using System.Security.Cryptography;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Import;

// Behaviour every IImportJobs must have: the job's state transitions, whatever runs the import. Callers are signed in as a user the arrange hooks know.
[TestFixture]
public abstract class ImportJobsContract
{
    protected abstract IImportJobs Jobs { get; }

    // A Jira connection Start may use.
    protected abstract Task<Guid> ArrangeConnectionAsync();

    // A project with no work items, as the target of an import into an existing project.
    protected abstract Task<Guid> ArrangeEmptyProjectAsync();

    // A project with work items already in it.
    protected abstract Task<Guid> ArrangeNonEmptyProjectAsync();

    // The key of a project that already exists.
    protected abstract Task<string> ArrangeTakenProjectKeyAsync();

    // A job in this state with that many pending errors, whose retry would succeed. Done and DoneWithErrors, Stopped and Failed have a finish time.
    protected abstract Task<Guid> ArrangeJobAsync(Guid connectionId, ImportJobState state, int pendingErrors);

    [Test]
    public async Task Start_ShouldQueueTheJob()
    {
        var job = await Jobs.StartAsync(await NewRequestAsync("PLATFORM"));

        job.Status.ShouldBe(ImportJobState.Queued);
        job.JiraProjectKey.ShouldBe("PLATFORM");
        job.Progress.Percent.ShouldBe(0);
        job.Progress.Step.ShouldBeNull();
        job.Progress.Rerun.ShouldBeFalse();
        job.FinishedAt.ShouldBeNull();
        job.CreatedBy.ShouldNotBeNull();
        (await Jobs.GetAsync(job.Id)).ShouldBe(job);
    }

    [Test]
    public async Task Start_WhenIntoAnExistingProject_ShouldKeepItsId()
    {
        var projectId = await ArrangeEmptyProjectAsync();
        var request = (await NewRequestAsync("PLATFORM")) with { Target = new ImportTarget(ImportTargetKind.ExistingProject, null, null, projectId) };

        var job = await Jobs.StartAsync(request);

        job.ProjectId.ShouldBe(projectId);
    }

    [TestCase("")]
    [TestCase("P")]
    [TestCase("PLATFORM")]
    [TestCase("pl")]
    [TestCase("P1AT")]
    public async Task Start_WhenKeyInvalid_ShouldBeValidation(string key)
    {
        var request = (await NewRequestAsync("PLATFORM")) with { Target = new ImportTarget(ImportTargetKind.NewProject, key, "Platform", null) };

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.StartAsync(request));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Start_WhenJiraProjectBlank_ShouldBeValidation()
    {
        var error = await Should.ThrowAsync<ApiException>(async () => await Jobs.StartAsync(await NewRequestAsync(" ")));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Start_WhenConnectionUnknown_ShouldBeNotFound()
    {
        var request = (await NewRequestAsync("PLATFORM")) with { ConnectionId = Guid.NewGuid() };

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.StartAsync(request));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Start_WhenExistingProjectUnknown_ShouldBeNotFound()
    {
        var request = (await NewRequestAsync("PLATFORM")) with { Target = new ImportTarget(ImportTargetKind.ExistingProject, null, null, Guid.NewGuid()) };

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.StartAsync(request));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Start_WhenTheSameJiraProjectIsAlreadyQueued_ShouldBeConflict()
    {
        var connection = await ArrangeConnectionAsync();
        await Jobs.StartAsync(await NewRequestAsync("PLATFORM", connection));

        var error = await Should.ThrowAsync<ApiException>(async () => await Jobs.StartAsync(await NewRequestAsync("PLATFORM", connection)));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Start_WhenKeyTaken_ShouldBeConflict()
    {
        var taken = await ArrangeTakenProjectKeyAsync();
        var request = (await NewRequestAsync("PLATFORM")) with { Target = new ImportTarget(ImportTargetKind.NewProject, taken, "Platform", null) };

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.StartAsync(request));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Start_WhenTargetProjectNotEmpty_ShouldBeConflict()
    {
        var projectId = await ArrangeNonEmptyProjectAsync();
        var request = (await NewRequestAsync("PLATFORM")) with { Target = new ImportTarget(ImportTargetKind.ExistingProject, null, null, projectId) };

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.StartAsync(request));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Get_WhenUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Jobs.GetAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task List_WhenFilteredByProject_ShouldReturnOnlyThatProjectsJobsNewestFirst()
    {
        var projectId = await ArrangeEmptyProjectAsync();
        var connection = await ArrangeConnectionAsync();
        var first = await ArrangeJobAsync(connection, ImportJobState.Done, 0);
        var request = (await NewRequestAsync("PLATFORM", connection)) with { Target = new ImportTarget(ImportTargetKind.ExistingProject, null, null, projectId) };
        var inProject = await Jobs.StartAsync(request);
        await Jobs.StopAsync(inProject.Id);
        var second = await Jobs.StartAsync(request);

        var listed = await Jobs.ListAsync(projectId);

        listed.Select(j => j.Id).ShouldBe([second.Id, inProject.Id]);
        (await Jobs.ListAsync(null)).Select(j => j.Id).ShouldContain(first);
    }

    [TestCase(ImportJobState.Queued)]
    [TestCase(ImportJobState.Running)]
    public async Task Stop_WhenNotFinished_ShouldStopOrBeStopping(ImportJobState state)
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), state, 0);

        var job = await Jobs.StopAsync(id);

        job.Status.ShouldBeOneOf(ImportJobState.Running, ImportJobState.Stopped);
        if (state == ImportJobState.Queued)
        {
            job.Status.ShouldBe(ImportJobState.Stopped);
        }
    }

    [TestCase(ImportJobState.Done)]
    [TestCase(ImportJobState.DoneWithErrors)]
    [TestCase(ImportJobState.Stopped)]
    [TestCase(ImportJobState.Failed)]
    public async Task Stop_WhenFinished_ShouldBeConflict(ImportJobState state)
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), state, 0);

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.StopAsync(id));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Skip_WhenLastErrorSkipped_ShouldMoveDoneWithErrorsToDone()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.DoneWithErrors, 1);
        var error = (await Jobs.GetErrorsAsync(id)).Single();

        var job = await Jobs.SkipAsync(error.Id);

        job.Status.ShouldBe(ImportJobState.Done);
        (await Jobs.GetErrorsAsync(id)).Single().Resolution.ShouldBe(ImportErrorResolution.Skipped);
    }

    [Test]
    public async Task Retry_WhenLastErrorRetried_ShouldMoveDoneWithErrorsToDone()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.DoneWithErrors, 1);
        var error = (await Jobs.GetErrorsAsync(id)).Single();

        var job = await Jobs.RetryAsync(error.Id);

        job.Status.ShouldBe(ImportJobState.Done);
        (await Jobs.GetErrorsAsync(id)).Single().Resolution.ShouldBe(ImportErrorResolution.Retried);
    }

    [Test]
    public async Task Skip_WhenOtherErrorsStayPending_ShouldStayDoneWithErrors()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.DoneWithErrors, 2);
        var errors = await Jobs.GetErrorsAsync(id);

        var job = await Jobs.SkipAsync(errors[0].Id);

        job.Status.ShouldBe(ImportJobState.DoneWithErrors);
    }

    [Test]
    public async Task GetErrors_ShouldListPendingOnesBeforeResolvedOnes()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.DoneWithErrors, 3);
        var before = await Jobs.GetErrorsAsync(id);
        await Jobs.SkipAsync(before[0].Id);

        var after = await Jobs.GetErrorsAsync(id);

        after.Select(e => e.Resolution).ShouldBe([ImportErrorResolution.Pending, ImportErrorResolution.Pending, ImportErrorResolution.Skipped]);
        after[2].Id.ShouldBe(before[0].Id);
    }

    [Test]
    public async Task RetryAll_ShouldResolveEveryPendingErrorAndFinish()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.DoneWithErrors, 3);

        var job = await Jobs.RetryAllAsync(id);

        job.Status.ShouldBe(ImportJobState.Done);
        (await Jobs.GetErrorsAsync(id)).ShouldAllBe(e => e.Resolution == ImportErrorResolution.Retried);
    }

    [Test]
    public async Task RetryAll_WhenNothingIsPending_ShouldBeConflict()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.Done, 0);

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.RetryAllAsync(id));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Skip_WhenAlreadyResolved_ShouldBeConflict()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.DoneWithErrors, 2);
        var errors = await Jobs.GetErrorsAsync(id);
        await Jobs.SkipAsync(errors[0].Id);

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.SkipAsync(errors[0].Id));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Skip_WhenErrorUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Jobs.SkipAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Retry_WhenErrorUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Jobs.RetryAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Skip_WhenJobIsStillRunning_ShouldNotFinishIt()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.Running, 1);
        var error = (await Jobs.GetErrorsAsync(id)).Single();

        var job = await Jobs.SkipAsync(error.Id);

        job.Status.ShouldBe(ImportJobState.Running);
    }

    [TestCase(ImportJobState.Done)]
    [TestCase(ImportJobState.DoneWithErrors)]
    [TestCase(ImportJobState.Stopped)]
    [TestCase(ImportJobState.Failed)]
    public async Task Rerun_ShouldQueueTheSameJob(ImportJobState state)
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), state, 0);

        var job = await Jobs.RerunAsync(id, null);

        job.Id.ShouldBe(id);
        job.Status.ShouldBe(ImportJobState.Queued);
        job.Progress.Rerun.ShouldBeTrue();
        job.Progress.Percent.ShouldBe(0);
        job.FinishedAt.ShouldBeNull();
        (await Jobs.ListAsync(null)).Count(j => j.Id == id).ShouldBe(1);
    }

    [TestCase(ImportJobState.Queued)]
    [TestCase(ImportJobState.Running)]
    public async Task Rerun_WhenStillGoing_ShouldBeConflict(ImportJobState state)
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), state, 0);

        var error = await Should.ThrowAsync<ApiException>(() => Jobs.RerunAsync(id, null));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Rerun_WhenMappingGiven_ShouldQueueTheSameJob()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.Done, 0);
        var edited = MappingWith(new ImportFieldsDto("customfield_10028", [], false, true, true, true));

        var job = await Jobs.RerunAsync(id, edited);

        job.Id.ShouldBe(id);
        job.Status.ShouldBe(ImportJobState.Queued);
        job.Progress.Rerun.ShouldBeTrue();
    }

    [Test]
    public async Task Rerun_WhenErrorsArePending_ShouldCarryThemOverAsPending()
    {
        var id = await ArrangeJobAsync(await ArrangeConnectionAsync(), ImportJobState.DoneWithErrors, 2);

        var job = await Jobs.RerunAsync(id, null);

        job.Status.ShouldBe(ImportJobState.Queued);
        var errors = await Jobs.GetErrorsAsync(id);
        errors.Count.ShouldBe(2);
        errors.ShouldAllBe(e => e.Resolution == ImportErrorResolution.Pending);
    }

    [Test]
    public async Task Rerun_WhenMappingGiven_ShouldSaveIt()
    {
        var started = await Jobs.StartAsync(await NewRequestAsync("PLATFORM"));
        await Jobs.StopAsync(started.Id);
        var edited = MappingWith(new ImportFieldsDto("customfield_10028", [], false, true, false, true));

        await Jobs.RerunAsync(started.Id, edited);

        var saved = (await Jobs.GetMappingAsync(started.Id)).Fields;
        saved.StoryPointsFieldId.ShouldBe("customfield_10028");
        saved.IncludeComponents.ShouldBeFalse();
        saved.IncludeAttachments.ShouldBeFalse();
    }

    [Test]
    public async Task Rerun_WhenMappingNull_ShouldKeepTheSavedOne()
    {
        var request = await NewRequestAsync("PLATFORM");
        var started = await Jobs.StartAsync(request with { Mapping = MappingWith(new ImportFieldsDto("customfield_10028", [], false, true, true, true)) });
        await Jobs.StopAsync(started.Id);

        await Jobs.RerunAsync(started.Id, null);

        var kept = (await Jobs.GetMappingAsync(started.Id)).Fields;
        kept.StoryPointsFieldId.ShouldBe("customfield_10028");
        kept.IncludeComponents.ShouldBeFalse();
    }

    [Test]
    public async Task GetMapping_ShouldReturnWhatTheJobStartedWith()
    {
        var started = await Jobs.StartAsync(await NewRequestAsync("PLATFORM"));

        var mapping = await Jobs.GetMappingAsync(started.Id);

        mapping.Fields.StoryPointsFieldId.ShouldBe("customfield_10016");
    }

    [Test]
    public async Task GetMapping_WhenJobUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Jobs.GetMappingAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Rerun_WhenJobUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Jobs.RerunAsync(Guid.NewGuid(), null));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task GetLog_AfterStart_ShouldHaveALine()
    {
        var job = await Jobs.StartAsync(await NewRequestAsync("PLATFORM"));

        var log = await Jobs.GetLogAsync(job.Id);

        log.ShouldNotBeEmpty();
        log.Select(l => l.Id).ShouldBe(log.Select(l => l.Id).Order());
        log[0].TimeLabel.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GetLog_WhenJobUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Jobs.GetLogAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    private async Task<ImportStartRequest> NewRequestAsync(string jiraKey, Guid? connection = null)
    {
        var target = new ImportTarget(ImportTargetKind.NewProject, RandomKey(), "Platform Team", null);
        var fields = new ImportFieldsDto("customfield_10016", [], true, true, true, true);
        return new ImportStartRequest(connection ?? await ArrangeConnectionAsync(), jiraKey, target, MappingWith(fields));
    }

    private static ImportMappingDto MappingWith(ImportFieldsDto fields) => new([], [], [], fields, new ImportTotalsDto(0, 0, 0, 0, 0, 0), []);

    private static string RandomKey() => new([.. Enumerable.Range(0, 4).Select(_ => (char)('A' + RandomNumberGenerator.GetInt32(26)))]);
}
