using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Fakes.Import;

internal sealed class InMemoryImportJob
{
    public required Guid Id { get; init; }

    public required Guid ConnectionId { get; init; }

    public required string JiraProjectKey { get; init; }

    public required string ProjectKey { get; init; }

    public Guid? ProjectId { get; set; }

    public ImportJobState Status { get; set; }

    public required ImportJobProgressJson Progress { get; set; }

    public required ImportMappingDto Mapping { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public Guid? CreatedBy { get; init; }

    public IList<InMemoryImportError> Errors { get; } = new List<InMemoryImportError>();

    public IList<InMemoryImportLog> Log { get; } = new List<InMemoryImportLog>();
}
