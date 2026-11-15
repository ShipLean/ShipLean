using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Tools.Inspection;

// Registers the Inspection filters from this module (Program.cs is frozen), as ToolErrorServices does. The call filter sits outside the
// error filter, so its not_found comes back as the tool error every other refusal is.
public sealed class InspectionToolServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddMcpServer().WithRequestFilters(filters =>
        {
            filters.AddListToolsFilter(InspectionToolFilter.ApplyToList);
            filters.AddCallToolFilter(InspectionToolFilter.ApplyToCall);
        });
}
