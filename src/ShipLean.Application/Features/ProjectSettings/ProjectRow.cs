using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.ProjectSettings;

// The project columns the settings page shows; Dapper maps the columns onto the constructor by name.
internal sealed record ProjectRow(
    string Key, string Name, string Description, string WorkingDays, int VelocityWindow, decimal? VelocityOverride,
    Guid? AgentConnectionId, string? RepoUrl, string? RepoBaseBranch, string Naming, string? HolidayCountry)
{
    public const string Select =
        "SELECT key, name, description, array_to_string(working_days, ',') AS workingDays, velocity_window AS velocityWindow, velocity_override AS velocityOverride, "
        + "agent_connection_id AS agentConnectionId, repo_url AS repoUrl, repo_base_branch AS repoBaseBranch, sprint_naming AS naming, holiday_country::text AS holidayCountry FROM project WHERE id = @projectId";

    public ProjectGeneralSettings General(IReadOnlyList<AgentConnectionOption> agentConnections)
        => new(Key, Name, Description, [.. WorkingDays.Split(',').Select(int.Parse).Order()], AgentConnectionId, RepoUrl, RepoBaseBranch, HolidayCountry, agentConnections);

    public ProjectForecastSettings Forecast => new(VelocityWindow, VelocityOverride, Naming == "yearQuarterNumber" ? SprintNaming.YearQuarterNumber : SprintNaming.Number);
}
