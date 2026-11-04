namespace ShipLean.Application.Features.Demos.Runs;

// Thrown inside the demo item's create when another caller already set the sprint's demo item: the create rolls back and the loser reads the winner's.
public sealed class DemoItemTakenException : Exception
{
}
