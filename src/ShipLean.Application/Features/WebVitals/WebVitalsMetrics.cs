using System.Diagnostics.Metrics;
using Agoda.IoC.Core;

namespace ShipLean.Application.Features.WebVitals;

// The web vitals counters that outlive a request, created once: a reading's metric dropped for being out of range.
[RegisterSingleton]
public sealed class WebVitalsMetrics(IMeterFactory meterFactory)
{
    public const string DiscardedCounterName = "shiplean.web_vitals.discarded";

    public Counter<long> Discarded { get; } = meterFactory.Create(WebVitalsQueue.MeterName).CreateCounter<long>(DiscardedCounterName);
}
