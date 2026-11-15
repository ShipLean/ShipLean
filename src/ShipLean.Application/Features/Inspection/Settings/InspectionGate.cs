using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Inspection.Settings;

// The two off switches (K5): the instance's, which makes every route not_found, and the project's, which makes the runs routes not_found.
[RegisterPerRequest]
public sealed class InspectionGate(InspectionOptions options, InspectionProjectStore store)
{
    public InspectionOptions Instance
    {
        get
        {
            RequireInstanceOn(options);
            return options;
        }
    }

    // Runs routes: the instance and the project must be on, and the caller a member.
    public async Task<InspectionProjectStore.ProjectSettings> RequireRunsAsync(string projectKey, CancellationToken ct)
    {
        RequireInstanceOn(options);
        var settings = await store.GetAsync(projectKey, ProjectRole.Member, ct);
        return settings.Enabled ? settings : throw ApiException.NotFound("Inspection is off for this project.");
    }

    private static void RequireInstanceOn(InspectionOptions options)
    {
        if (!options.Enabled)
        {
            throw ApiException.NotFound("Inspection is off.");
        }
    }
}
