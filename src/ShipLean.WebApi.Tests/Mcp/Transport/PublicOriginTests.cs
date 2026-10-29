using Microsoft.Extensions.Logging;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// The process-wide note that agent URLs fell back to a cluster service name: one warning, and the /health flag.
[TestFixture]
public class PublicOriginTests
{
    [Test]
    public void Note_WhenTheFallbackRepeats_ShouldWarnOnce()
    {
        var logs = new Warnings();
        var origin = new PublicOrigin(logs);
        const string reason = "peer 203.0.113.8 is not in SHIPLEAN_TRUSTED_PROXIES";

        origin.Note(baseUrlBlank: true, reason);
        origin.Note(baseUrlBlank: true, reason);

        origin.HealthValue.ShouldBe(PublicOrigin.Unknown);
        logs.Count.ShouldBe(1);
        logs.Line.ShouldContain(reason);
        logs.Line.ShouldContain("Set base_url");
    }

    [Test]
    public void Note_WhenBaseUrlIsSet_ShouldClearTheFlag()
    {
        var origin = new PublicOrigin(new Warnings());
        origin.Note(baseUrlBlank: true, "no X-Forwarded-Host");

        origin.Note(baseUrlBlank: false, clusterFallback: null);

        origin.HealthValue.ShouldBeNull();
    }

    [Test]
    public void Clear_WhenCalled_ShouldClearTheFlag()
    {
        var origin = new PublicOrigin(new Warnings());
        origin.Note(baseUrlBlank: true, "no X-Forwarded-Host");

        origin.Clear();

        origin.HealthValue.ShouldBeNull();
    }

    private sealed class Warnings : ILogger<PublicOrigin>
    {
        private readonly List<string> _lines = [];

        public int Count => _lines.Count;

        public string Line => _lines[0];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                _lines.Add(formatter(state, exception));
            }
        }
    }
}
