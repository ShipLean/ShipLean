namespace ShipLean.Contracts.WorkItems.Reads.Tree;

// A level of the backlog stack. Requirement = Stories and Bugs together; then Feature; then Epic.
public enum StackLevel
{
    Requirement,
    Feature,
    Epic,
}
