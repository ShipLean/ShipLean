namespace ShipLean.WebApi.Tests.Instance.AgentConnections;

// Connections the allowed leg of the access sweep finds already there (AllowedSweepTests inserts them: the seed has none).
// The archive row has an id of its own, so no other row meets an archived connection.
internal static class AgentConnectionSweepIds
{
    public static readonly Guid Live = Guid.Parse("0a1c0000-0000-7000-8000-000000000001");
    public static readonly Guid ToArchive = Guid.Parse("0a1c0000-0000-7000-8000-000000000002");
}
