namespace ShipLean.WebApi.Features.Import.Jobs;

// PreviousJobId: the job whose saved choices a re-run keeps.
public sealed record ImportMappingRequest(Guid? ConnectionId, string JiraProjectKey, Guid? PreviousJobId);
