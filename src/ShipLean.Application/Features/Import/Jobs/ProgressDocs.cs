using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Import.Jobs;

public static class ProgressDocs
{
    public static readonly ImportEntityCount None = new(0, 0, 0);

    public static ImportJobProgressJson Initial(bool rerun)
        => new(0, null, "Queued", rerun, new ImportCounts(None, None, None, None, None), 0, 0, 0, null);
}
