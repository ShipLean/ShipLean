namespace ShipLean.Contracts.Import;

// The mapping the stepper starts from. Callers check project access first. Throws ApiException.NotFound for an unknown connection, Jira project or previous job, and the IJiraClient errors when Jira refuses or is down.
public interface IImportMappings
{
    // Suggested types, statuses, users and fields for the Jira project. With a previous job (decision: a re-run keeps earlier choices), every row that job's mapping covered carries that job's saved choices (issue type targets, status targets, user resolutions including "same person as", the story points field and the include options), and only rows it did not cover (a status, user or issue type that appeared in Jira since) get the defaults and IsNewSinceLastRun true; otherwise every flag is false.
    Task<ImportMappingDto> SuggestAsync(Guid connectionId, string jiraProjectKey, Guid? previousJobId, CancellationToken cancellationToken = default);
}
