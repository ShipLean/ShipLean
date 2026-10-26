using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Instance.Integrations;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// Shared by the integrations tests: the wire calls, and the settings the link sources would read.
internal static class IntegrationsWire
{
    public static readonly IReadOnlyDictionary<string, string?> Env = new Dictionary<string, string?>
    {
        ["SHIPLEAN_GITHUB_TOKEN"] = "ghp_envsecret0001",
        ["SHIPLEAN_GITLAB_URL"] = "https://gitlab.env.example.com",
        ["SHIPLEAN_GITLAB_TOKEN"] = "glpat-envsecret0002",
    };

    public static async Task<(IntegrationsSettingsDto Dto, string Text)> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return (JsonSerializer.Deserialize<IntegrationsSettingsDto>(text, JsonSerializerOptions.Web)!, text);
    }

    public static async Task<IntegrationsSettingsDto> GetAsync(ApiClient api) => (await ReadAsync(await api.GetAsync("api/instance/integrations"))).Dto;

    public static async Task<IntegrationSettingsDto> ResolveAsync(ShipLeanHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntegrationSettings>().GetAsync();
    }
}
