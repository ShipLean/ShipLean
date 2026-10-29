using Dapper;
using System.Data.Common;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Sprints;

// The project columns the capacity maths reads.
public sealed record CapacityProject(string WorkingDays, string? HolidayCountry, decimal? VelocityOverride)
{
    public IReadOnlyList<int> Days => CapacityCalendar.ParseWorkingDays(WorkingDays);

    public static async Task<CapacityProject> LoadAsync(DbConnection connection, DbTransaction? transaction, Guid projectId, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<CapacityProject>(new CommandDefinition(
            "SELECT array_to_string(working_days, ',') AS workingDays, holiday_country::text AS holidayCountry, velocity_override AS velocityOverride FROM project WHERE id = @projectId",
            new { projectId }, transaction, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("There is no such project.");
}
