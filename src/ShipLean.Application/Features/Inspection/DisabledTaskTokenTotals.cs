using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection;

// The ITaskTokenTotals while SHIPLEAN_INSPECTION is off. Inspection's Cost view asks IInspectionFlag first and never reads it then;
// it exists so the port always resolves.
public sealed class DisabledTaskTokenTotals : ITaskTokenTotals
{
    public const string Off = "Inspection is off (SHIPLEAN_INSPECTION).";

    public Task<TaskTokenTotals> ReadAsync(TaskTokenQuery query, CancellationToken ct) => Task.FromResult(TaskTokenTotals.Unavailable(Off));
}
