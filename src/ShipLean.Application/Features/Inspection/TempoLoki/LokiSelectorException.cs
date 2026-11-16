namespace ShipLean.Application.Features.Inspection.TempoLoki;

// The project's extra Loki matchers are not plain label matchers.
public sealed class LokiSelectorException(string message) : Exception(message)
{
}
