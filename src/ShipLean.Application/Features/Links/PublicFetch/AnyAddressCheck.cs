using System.Net;
using ShipLean.Application.Http;

namespace ShipLean.Application.Features.Links.PublicFetch;

// Allows every address. Only the allowed-host client uses it, and only for hosts on PreviewAllowHosts.
internal sealed class AnyAddressCheck : IAddressCheck
{
    public bool IsAllowed(IPAddress address) => true;
}
