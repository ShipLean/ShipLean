using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Retros.People;

// The host and helpers the people's retro write tests share. Every test arranges a project of its own with Joel as a member,
// so the themes, retros and counts it asserts on are its alone.
public abstract class RetroPeopleFixture : ApiFixture
{
    protected ProjectEventWatch Watch(Guid projectId) => new(Host.Services.GetRequiredService<IProjectEvents>(), projectId);

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    protected async Task<JsonElement> PostPointAsync(Guid projectId, object body)
    {
        using var response = await Api.PostJsonAsync($"/api/projects/{projectId}/retros/points", body);
        return await ReadAsync(response, HttpStatusCode.Created);
    }
}
