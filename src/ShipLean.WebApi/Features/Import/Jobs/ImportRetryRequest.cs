namespace ShipLean.WebApi.Features.Import.Jobs;

// ErrorId null retries every pending error of the job.
public sealed record ImportRetryRequest(Guid? ErrorId);
