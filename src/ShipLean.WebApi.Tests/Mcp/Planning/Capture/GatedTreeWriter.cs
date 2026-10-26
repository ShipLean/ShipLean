using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Capture;

// A test double that wraps the real tree writer and holds each write at the gate a test armed before it goes on.
public sealed class GatedTreeWriter(IWorkItemTreeWriter inner, TreeGate gate) : IWorkItemTreeWriter
{
    public async Task<WorkItemTreeOutcome> CreateTreeAsync(WorkItemTreeCreate tree, WriteOptions options, CancellationToken cancellationToken = default)
    {
        await gate.PassAsync();
        return await inner.CreateTreeAsync(tree, options, cancellationToken);
    }
}
