using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Settings;

// A project's Inspection settings. They answer while the project is off (an admin turns Inspection on here); only the instance switch hides them.
[RegisterPerRequest]
public sealed class InspectionSettingsService(InspectionGate gate, InspectionProjectStore store)
{
    public async Task<InspectionProjectSettingsDto> GetAsync(string projectKey, CancellationToken ct)
    {
        var options = gate.Instance;
        return Dto(await store.GetAsync(projectKey, ProjectRole.Member, ct), options);
    }

    public async Task<InspectionProjectSettingsDto> PutAsync(string projectKey, InspectionProjectSettingsDto request, CancellationToken ct)
    {
        var options = gate.Instance;
        var current = await store.GetAsync(projectKey, ProjectRole.Admin, ct);
        var saved = current with
        {
            Enabled = request.Enabled,
            ShowContent = request.ShowContent,
            TenantOverride = Clean(request.TenantOverride),
            SelectorOverride = Clean(request.SelectorOverride),
        };
        if (saved.SelectorOverride is { } selector && !selector.StartsWith('{'))
        {
            throw ApiException.Validation("The selector must be a stream selector in braces.", "selectorOverride");
        }
        await store.SaveAsync(saved, ct);
        return Dto(saved, options);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static InspectionProjectSettingsDto Dto(InspectionProjectStore.ProjectSettings s, InspectionOptions options)
        => new(s.Enabled, s.ShowContent, s.TenantOverride, s.SelectorOverride, options.Store == InspectionOptionsReader.ClickHouse ? "clickhouse" : "tempoLoki");
}
