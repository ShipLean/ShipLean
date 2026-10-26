using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

// A type's states by category, from the project's catalog (type order, then state order).
internal static class WorkStates
{
    public static StateDto First(IReadOnlyList<StateDto> catalog, WorkItemTypeId type, StateCategory category)
        => FirstOrDefault(catalog, type, category)
           ?? throw ApiException.Validation($"A {type} in this project has no {category} state.");

    public static StateDto? FirstOrDefault(IReadOnlyList<StateDto> catalog, WorkItemTypeId type, StateCategory category)
        => catalog.Where(s => s.Type == type && s.Category == category).OrderBy(s => s.Order).FirstOrDefault();

    public static StateCategory CategoryOf(IReadOnlyList<StateDto> catalog, Guid stateId)
        => catalog.Single(s => s.Id == stateId).Category;
}
