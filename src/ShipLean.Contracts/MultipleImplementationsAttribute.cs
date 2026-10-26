namespace ShipLean.Contracts;

// Marks a contract interface that is meant to have many implementations registered (a family of handlers, say), so
// ServiceModuleTests does not require exactly one.
[AttributeUsage(AttributeTargets.Interface)]
public sealed class MultipleImplementationsAttribute : Attribute
{
}
