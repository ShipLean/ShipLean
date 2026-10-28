using System.Text.Json;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.States;

// One live state as the queries return it; Dapper maps the columns onto the constructor by name.
internal sealed record StateRow(Guid Id, string Type, string Name, string Category, int SortOrder, int ItemCount, bool StartsAgent = false)
{
    public const string Columns = "s.id, s.type, s.name, s.category, s.sort_order AS sortOrder, 0 AS itemCount, s.starts_agent AS startsAgent";

    public WorkItemTypeId TypeId => Enum.Parse<WorkItemTypeId>(Type, ignoreCase: true);

    public StateCategory CategoryId => Enum.Parse<StateCategory>(Category, ignoreCase: true);

    public StateDto ToDto() => new(Id, TypeId, Name, CategoryId, SortOrder, ItemCount, StartsAgent);

    public static string TypeText(WorkItemTypeId type) => type.ToString().ToLowerInvariant();

    public static string CategoryText(StateCategory category) => JsonNamingPolicy.CamelCase.ConvertName(category.ToString());
}
