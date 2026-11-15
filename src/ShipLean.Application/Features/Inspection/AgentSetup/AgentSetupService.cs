using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Settings;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.AgentSetup;

// What an agent needs to send telemetry for one item (D24). Works while the project is off, so an admin can set agents up before turning it on.
[RegisterPerRequest]
public sealed class AgentSetupService(InspectionGate gate, InspectionProjectStore store, IInspectionItemLookup items)
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["claude-code"] = "Claude Code",
        ["codex-cli"] = "Codex CLI",
        ["cursor"] = "Cursor",
    };

    public async Task<AgentSetupDto> GetAsync(string projectKey, string agent, string? item, CancellationToken ct)
    {
        var options = gate.Instance;
        await store.GetAsync(projectKey, ProjectRole.Member, ct);
        if (!Labels.TryGetValue(agent, out var label))
        {
            throw ApiException.Validation("agent must be claude-code, codex-cli or cursor.", "agent");
        }
        var itemKey = item is null ? "SHIP-123" : (await items.RequireAsync(projectKey, item, ct)).ItemKey;

        // TODO: the real files and notes for each agent, from the embedded templates under AgentSetup/.
        var content = $"OTEL_EXPORTER_OTLP_ENDPOINT={options.AgentOtlpEndpoint ?? "<your collector>"}\nOTEL_RESOURCE_ATTRIBUTES={options.Fields.Item}={itemKey},{options.Fields.Project}={projectKey}\n";
        return new AgentSetupDto(agent, label, [new AgentSetupFileDto("telemetry.env", "dotenv", content)], [], $"https://docs.shiplean.app/inspection/{agent}");
    }
}
