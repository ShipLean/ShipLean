using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Application.Events;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Mcp.Transport;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Demos;

// What the run's two real-path test classes share: the doubles for the data and the prompt, a project with a closed sprint, and the two calls.
internal static class DemoRunCalls
{
    public static void UseNamedDataAndUrlPrompt(IServiceCollection services, UploadUrlPrompt prompt)
    {
        services.RemoveAll<ISprintDemoData>();
        services.AddScoped<ISprintDemoData, SprintNamedDemoData>();
        services.RemoveAll<ISprintDemoPrompt>();
        services.AddSingleton<ISprintDemoPrompt>(prompt);
    }

    // Replaces the brand source with one that returns the given brand.
    public static void UseBrand(IServiceCollection services, DemoBrand brand)
    {
        services.RemoveAll<IDemoBrandSource>();
        services.AddSingleton<IDemoBrandSource>(new FixedBrandSource(brand));
    }

    // Wraps the host's bus so published events can be read at once, without waiting for delivery.
    public static void UseRecordedEvents(IServiceCollection services, Action<TeeProjectEvents> capture)
    {
        services.RemoveAll<IProjectEvents>();
        services.AddSingleton<IProjectEvents>(provider =>
        {
            var tee = new TeeProjectEvents(ActivatorUtilities.CreateInstance<InMemoryProjectEvents>(provider));
            capture(tee);
            return tee;
        });
    }

    // Joel is admin. An agent connection and a repository are set unless left out. The sprint is closed (rollover decided) unless left open.
    public static async Task<(ArrangedProject Project, Guid SprintId, string SprintName)> ArrangeAsync(NpgsqlConnection database, ApiClient api, bool agent = true, bool closed = true)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, api.ActingAs(TestUser.Joel), connection: agent, repository: agent);
        var name = $"Spr {Guid.NewGuid():N}"[..14];
        var sprintId = await DemoRunArrange.NewSprintAsync(database, arranged.Project.Id, name);
        if (closed)
        {
            await database.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @sprintId", new { sprintId });
        }

        return (arranged.Project, sprintId, name);
    }

    public static Task<HttpResponseMessage> ButtonAsync(ApiClient api, Guid sprintId) => api.ActingAs(TestUser.Joel).PostJsonAsync($"api/sprints/{sprintId}/demo");

    public static string ToolArguments(string projectKey, string sprintName) => $$"""{"project":"{{projectKey}}","sprint":"{{sprintName}}"}""";

    public static async Task<Guid?> ItemOfAsync(NpgsqlConnection database, Guid sprintId)
        => await database.ExecuteScalarAsync<Guid?>("SELECT demo_work_item_id FROM sprint WHERE id = @sprintId", new { sprintId });

    public static async Task EndSessionsAsync(NpgsqlConnection database, Guid itemId)
        => await database.ExecuteAsync("UPDATE agent_session SET state = 'completed' WHERE work_item_id = @itemId", new { itemId });

    // Unused, unexpired, unrevoked demo links of the item: the ones that could still be used.
    public static async Task<int> LiveTokensAsync(NpgsqlConnection database, Guid itemId)
        => await database.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM transfer_token WHERE work_item_id = @itemId AND kind = 'demo' AND revoked_at IS NULL AND used_at IS NULL AND expires_at > now()", new { itemId });

    public static async Task<int> TokensAsync(NpgsqlConnection database, Guid itemId)
        => await database.ExecuteScalarAsync<int>("SELECT count(*) FROM transfer_token WHERE work_item_id = @itemId AND kind = 'demo'", new { itemId });

    public static async Task<string> DeliveredBodyAsync(NpgsqlConnection database, Guid itemId)
        => (await AgentDispatchArrange.DeliveriesAsync(database, itemId))[^1].Body;

    public static bool IsConflict(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Conflict;
}
