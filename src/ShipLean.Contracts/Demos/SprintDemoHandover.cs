namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoHandover(
    Guid SprintId,
    string ItemKey,
    string ItemUrl,
    string SprintUrl,
    string UploadUrl,
    DateTimeOffset UploadExpiresAt,
    long MaxVideoBytes,
    DemoBrand Brand,
    string Instructions);
