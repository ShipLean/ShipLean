namespace ShipLean.Application.Features.WorkItems.Rules;

// What the rules read through the write's transaction: a state's name and category.
internal sealed record RuleState(Guid Id, string Name, string Category);
