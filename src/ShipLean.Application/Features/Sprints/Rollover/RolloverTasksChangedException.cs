namespace ShipLean.Application.Features.Sprints;

// Thrown inside the rollover's transaction when a moved story's open tasks are not the ones the attempt planned; the attempt starts over.
public sealed class RolloverTasksChangedException() : Exception("The sprint's tasks changed while the rollover was applied.")
{
}
