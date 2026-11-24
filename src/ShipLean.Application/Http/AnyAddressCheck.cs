using System.Net;

namespace ShipLean.Application.Http;

// Allows every address. Only for a host an instance admin named: one on PreviewAllowHosts, or the GitLab URL (a self-hosted
// GitLab is usually on a private network).
internal sealed class AnyAddressCheck : IAddressCheck
{
    public bool IsAllowed(IPAddress address) => true;
}
