using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Logging;
using ShipLean.TestSupport.Secrets;
using Shouldly;

namespace ShipLean.UnitTest.Harness;

// C11: the leak check must find a planted secret on every surface, in every form a URL can carry it, and pass when it is absent.
[TestFixture]
public class SecretLeakAssertTests
{
    private const string Secret = "shl_up_Zm9v+YmFy/YmF6";

    private static readonly SecretLeakAssert Leak = new(Secret);

    [Test]
    public void NotInLogs_WhenALogLineHoldsTheSecret_ShouldFail()
    {
        var logs = new RecordedLogs();
        logs.CreateLogger("ShipLean.Agents").LogWarning("Delivery to {Url} failed", $"https://relay.example.com/hook?token={Secret}");

        var error = Should.Throw<ShouldAssertException>(() => Leak.NotInLogs(logs));

        error.Message.ShouldContain("a log line");
        error.Message.ShouldNotContain(Secret);
    }

    [Test]
    public void NotInLogs_WhenNoLineHoldsTheSecret_ShouldPass()
    {
        var logs = new RecordedLogs();
        logs.CreateLogger("ShipLean.Agents").LogWarning("Delivery to {Url} failed", "https://relay.example.com/hook");

        Should.NotThrow(() => Leak.NotInLogs(logs));
    }

    [Test]
    public void NotInTraceAttributes_WhenATagHoldsTheEscapedSecret_ShouldFail()
    {
        using var activity = new Activity("POST /api/uploads/agent-log");
        activity.SetTag("url.query", "token=" + Uri.EscapeDataString(Secret));

        Should.Throw<ShouldAssertException>(() => Leak.NotInTraceAttributes(SecretLeakAssert.Attributes(activity)))
            .Message.ShouldContain("a trace attribute");
    }

    [Test]
    public void NotInTraceAttributes_WhenNoTagHoldsTheSecret_ShouldPass()
    {
        using var activity = new Activity("POST /api/uploads/agent-log");
        activity.SetTag("http.route", "/api/uploads/agent-log");

        Should.NotThrow(() => Leak.NotInTraceAttributes(SecretLeakAssert.Attributes(activity)));
    }

    [Test]
    public void NotInUsageEvents_WhenAnEventHoldsTheSecret_ShouldFail()
    {
        var usage = new UsageEvent(
            UsageEventNames.AgentSessionRunLogUploaded, UsageSurface.System, UsageActor.System, null, 0, UsageOutcome.Error, Secret, 5,
            "system:Test", DateTimeOffset.UnixEpoch, new Dictionary<string, object>());

        Should.Throw<ShouldAssertException>(() => Leak.NotInUsageEvents([usage])).Message.ShouldContain("a usage event");
    }

    [Test]
    public void NotInJson_WhenTheBodyHoldsTheFormEncodedSecret_ShouldFail()
    {
        var json = $$"""{"prompt": "curl {{System.Net.WebUtility.UrlEncode(Secret)}}"}""";

        Should.Throw<ShouldAssertException>(() => Leak.NotInJson(json)).Message.ShouldContain("the JSON body");
    }

    [Test]
    public void NotInMcpResult_WhenTheResultHoldsTheSecret_ShouldFail()
    {
        Should.Throw<ShouldAssertException>(() => Leak.NotInMcpResult(new { session = new { endDetail = Secret } }))
            .Message.ShouldContain("the MCP result");
    }

    [Test]
    public void NotInJson_WhenTheSecretIsAbsent_ShouldPass()
    {
        Should.NotThrow(() => Leak.NotInJson("""{"endReason": "deliveryFailed"}"""));
    }

    [Test]
    public void Constructor_WhenASecretIsEmpty_ShouldRefuseIt()
    {
        Should.Throw<ArgumentException>(() => new SecretLeakAssert(string.Empty));
    }
}
