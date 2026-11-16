using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Views;

// GET api/inspection/health, instance admin only. Answers while Inspection is off (enabled=false) so an admin can see why.
[RegisterPerRequest]
public sealed class InspectionHealthView(InspectionOptions options, ITraceSource source, IProjectAccess access)
{
    public async Task<InspectionHealthDto> GetAsync(CancellationToken ct)
    {
        await access.RequireInstanceAdminAsync(ct);
        if (!options.Enabled)
        {
            return new InspectionHealthDto(false, "off", false, null, null, null, []);
        }
        var health = await source.CheckAsync(ct);
        var fields = await source.CheckFieldsAsync(ct);
        return new InspectionHealthDto(
            true, options.Store == InspectionOptionsReader.ClickHouse ? "clickhouse" : "tempoLoki", health.Reachable, health.LatencyMs, health.ViewLagSeconds,
            health.Problem, [.. fields.Select(f => new InspectionFieldDto(f.Name, f.Attribute, f.Found))]);
    }
}
