using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Projects;

// StartsAgent: moving an item into the state opens an agent session (SHIP-178). Order is the position among the states of the same type; ItemCount counts the project's work items in the state.
public sealed record StateDto(Guid Id, WorkItemTypeId Type, string Name, StateCategory Category, int Order, int ItemCount, bool StartsAgent = false);
