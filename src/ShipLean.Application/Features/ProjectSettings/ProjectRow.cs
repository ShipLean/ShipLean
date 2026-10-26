using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.ProjectSettings;

// The project columns the settings page shows; Dapper maps the columns onto the constructor by name.
internal sealed record ProjectRow(string Key, string Name, string Description, string WorkingDays, int VelocityWindow, decimal? VelocityOverride)
{
    public const string Select =
        "SELECT key, name, description, array_to_string(working_days, ',') AS workingDays, velocity_window AS velocityWindow, velocity_override AS velocityOverride FROM project WHERE id = @projectId";

    public ProjectGeneralSettings General => new(Key, Name, Description, [.. WorkingDays.Split(',').Select(int.Parse).Order()]);

    public ProjectForecastSettings Forecast => new(VelocityWindow, VelocityOverride);
}
