namespace ShipLean.Contracts.Analytics;

// What a sink throws when a batch could not be delivered.
public sealed class AnalyticsSendException(string message, Exception? inner = null) : Exception(message, inner)
{
}
