using System.Text.Json;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Capacity;

// Reads the capacity the way the page's own request does, as the signed-in user.
public static class CapacityApi
{
    public sealed record Entry(string Member, decimal? Days, decimal Points, string? Note);

    public static async Task<JsonElement> ReadAsync(IPage page, string path)
    {
        var response = await page.APIRequest.GetAsync(path);
        response.Ok.ShouldBeTrue();
        return (await response.JsonAsync()).GetValueOrDefault();
    }

    public static async Task<Entry[]> EntriesAsync(IPage page, Guid sprintId)
    {
        var capacity = await ReadAsync(page, $"/api/sprints/{sprintId}/capacity");
        return capacity.GetProperty("members").EnumerateArray()
            .Select(m => new Entry(
                m.GetProperty("user").GetProperty("name").GetString()!,
                m.GetProperty("days").ValueKind == JsonValueKind.Null ? null : m.GetProperty("days").GetDecimal(),
                m.GetProperty("points").GetDecimal(),
                m.GetProperty("note").ValueKind == JsonValueKind.Null ? null : m.GetProperty("note").GetString()))
            .ToArray();
    }

    // W and W minus the holidays, as the server counted them for the sprint.
    public static async Task<(int WorkingDays, int DefaultDays)> CalendarAsync(IPage page, Guid sprintId)
    {
        var capacity = await ReadAsync(page, $"/api/sprints/{sprintId}/capacity");
        return (capacity.GetProperty("workingDays").GetInt32(), capacity.GetProperty("defaultDays").GetInt32());
    }

    // The same formula as the server and the page: velocity / members * days / working days, two decimals, half away from zero.
    public static decimal Share(decimal velocity, int members, decimal days, int workingDays) =>
        decimal.Round(velocity * Math.Min(days, workingDays) / (members * workingDays), 2, MidpointRounding.AwayFromZero);

    public static async Task<string[]> MemberNamesAsync(IPage page, string projectKey)
    {
        var project = await ReadAsync(page, $"/api/projects/by-key/{projectKey}");
        var members = await ReadAsync(page, $"/api/projects/{project.GetProperty("id").GetString()}/members");
        return members.EnumerateArray().Select(m => m.GetProperty("user").GetProperty("name").GetString()!).ToArray();
    }

    public static async Task<decimal> VelocityAsync(IPage page, string projectKey)
    {
        var project = await ReadAsync(page, $"/api/projects/by-key/{projectKey}");
        var velocity = await ReadAsync(page, $"/api/projects/{project.GetProperty("id").GetString()}/velocity");
        var overridden = velocity.GetProperty("override");
        return overridden.ValueKind == JsonValueKind.Null ? velocity.GetProperty("average").GetDecimal() : overridden.GetDecimal();
    }
}
