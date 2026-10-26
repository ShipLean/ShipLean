using OpenTelemetry.Trace;

namespace ShipLean.WebApi.Infrastructure.Telemetry;

// Everything is sampled (and a child follows its parent) except the presence heartbeat, which ticks every few seconds
// and would drown the traces. Its Redis commands are its children, so they go with it.
internal sealed class TraceSampling : Sampler
{
    public const string HeartbeatSpan = "presence.heartbeat";

    private readonly Sampler _inner = new ParentBasedSampler(new AlwaysOnSampler());

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        => samplingParameters.Name == HeartbeatSpan && samplingParameters.ParentContext.TraceId == default
            ? new SamplingResult(SamplingDecision.Drop)
            : _inner.ShouldSample(in samplingParameters);
}
