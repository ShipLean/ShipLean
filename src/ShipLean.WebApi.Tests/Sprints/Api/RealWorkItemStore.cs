using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// The real work item store (pass-through rules) for tests that wrap it in a decorator.
public static class RealWorkItemStore
{
    public static IWorkItemStore Build(IServiceProvider services) => new WorkItemStore(
        services.GetRequiredService<NpgsqlDataSource>(), services.GetRequiredService<ICurrentUser>(), new PassThroughWorkItemRules(),
        services.GetRequiredService<IProjectEvents>(), new MarkdownRenderer(), TimeProvider.System, AgentIdentity.None);
}
