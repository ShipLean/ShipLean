using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Settings;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.AgentSetup;

// What an agent needs to send telemetry for one item (D24). Works while the project is off, so an admin can set agents up before turning it on.
[RegisterPerRequest]
public sealed class AgentSetupService(InspectionGate gate, InspectionProjectStore store, IInspectionItemLookup items) : IAgentSetup
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
        var itemKey = item is null ? AgentSetupContent.ItemPlaceholder : (await items.RequireAsync(projectKey, item, ct)).ItemKey;

        var inputs = new AgentSetupContent.Inputs(projectKey, itemKey, options.AgentOtlpEndpoint, options.AgentOtlpHeaders, options.Fields);
        return AgentSetupContent.For(agent, label, inputs);
    }
}
