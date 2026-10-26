using System.Net;
using System.Net.Sockets;

namespace ShipLean.WebApi.Features.Agents.Clients;

// The rate-limit partition of an anonymous caller. An IPv4 caller is its address; an IPv6 caller is its /64, since one
// subscriber holds a whole /64 and could otherwise rotate through addresses to dodge the limit. Behind the proxy the
// forwarded-headers middleware has already put the real client address in RemoteIpAddress.
public static class ClientIpKey
{
    public static string For(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }
        var prefix = address.GetAddressBytes();
        Array.Clear(prefix, 8, 8);
        return new IPAddress(prefix) + "/64";
    }
}
