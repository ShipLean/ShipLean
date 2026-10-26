using System.Net;
using ShipLean.Application.Http;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// The real address rules with one hole: loopback, where StubHttpServer listens. Every other address is judged by PublicAddressCheck.
public sealed class LoopbackAllowedAddressCheck : IAddressCheck
{
    private readonly PublicAddressCheck _rules = new();

    public bool IsAllowed(IPAddress address) => IPAddress.IsLoopback(address) || _rules.IsAllowed(address);
}
