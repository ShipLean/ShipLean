using System.Net;
using Microsoft.Extensions.Time.Testing;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// Who shares a count (an IPv6 /64), when a count ends, and who cleans up (a timer).
[TestFixture]
public class AgentFailureLimiterTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    [Test]
    public void FailureLimiter_WhenAddressesShareASixtyFourBitPrefix_ShouldCountThemTogether()
    {
        using var limiter = new AgentFailureLimiter(_clock);

        for (var i = 1; i <= AgentPolicy.FailedLookupsPerMinutePerIp; i++)
        {
            limiter.TryRecordFailure(IPAddress.Parse($"2001:db8:1:2::{i:x}")).ShouldBeTrue();
        }

        limiter.TryRecordFailure(IPAddress.Parse("2001:db8:1:2:ffff::1")).ShouldBeFalse();
        limiter.TryRecordFailure(IPAddress.Parse("2001:db8:1:3::1")).ShouldBeTrue();
    }

    [Test]
    public void FailureLimiter_WhenAnIPv4AddressIsMappedToIPv6_ShouldCountItAsTheIPv4Address()
    {
        using var limiter = new AgentFailureLimiter(_clock);
        var plain = IPAddress.Parse("203.0.113.9");

        for (var i = 0; i < AgentPolicy.FailedLookupsPerMinutePerIp; i++)
        {
            limiter.TryRecordFailure(plain).ShouldBeTrue();
        }

        limiter.TryRecordFailure(plain.MapToIPv6()).ShouldBeFalse();
    }

    [Test]
    public void FailureLimiter_WhenTheMinuteEnds_ShouldStartANewCount()
    {
        using var limiter = new AgentFailureLimiter(_clock);
        var address = IPAddress.Parse("203.0.113.9");
        for (var i = 0; i <= AgentPolicy.FailedLookupsPerMinutePerIp; i++)
        {
            limiter.TryRecordFailure(address);
        }

        _clock.Advance(TimeSpan.FromSeconds(61));

        limiter.TryRecordFailure(address).ShouldBeTrue();
    }

    [Test]
    public void FailureLimiter_WhenWindowsHaveEnded_ShouldDropThemOnATimerNotOnFailures()
    {
        using var limiter = new AgentFailureLimiter(_clock);
        limiter.TryRecordFailure(IPAddress.Parse("203.0.113.1"));
        limiter.TryRecordFailure(IPAddress.Parse("203.0.113.2"));

        _clock.Advance(TimeSpan.FromSeconds(30));
        limiter.TryRecordFailure(IPAddress.Parse("203.0.113.3"));
        limiter.TrackedNetworks.ShouldBe(3);
        _clock.Advance(TimeSpan.FromSeconds(31));

        limiter.TrackedNetworks.ShouldBe(1);
    }
}
