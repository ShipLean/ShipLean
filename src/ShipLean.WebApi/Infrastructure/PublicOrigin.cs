using Agoda.IoC.Core;
using Microsoft.Extensions.Logging;

namespace ShipLean.WebApi.Infrastructure;

// One warning per process when a published agent URL falls back to a cluster service name, and the flag /health and the
// instance settings page read. A later request with base_url set clears it: that origin is the one agents open.
[RegisterSingleton]
public sealed class PublicOrigin(ILogger<PublicOrigin> logger)
{
    public const string Unknown = "unknown: set base_url";

    private int _warned;
    private string? _reason;

    public string? HealthValue => _reason is null ? null : Unknown;

    public void Note(bool baseUrlBlank, string? clusterFallback)
    {
        if (!baseUrlBlank)
        {
            _reason = null;
            return;
        }
        if (clusterFallback is null)
        {
            return;
        }
        _reason = clusterFallback;
        if (Interlocked.Exchange(ref _warned, 1) == 0)
        {
            logger.LogWarning(
                "Agent URLs fell back to a cluster service name ({Reason}). Set base_url, or add the gateway to SHIPLEAN_TRUSTED_PROXIES and send X-Forwarded-Host and X-Forwarded-Proto.",
                clusterFallback);
        }
    }

    public void Clear() => _reason = null;
}
