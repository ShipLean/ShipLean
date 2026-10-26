using System.Net;
using System.Net.Sockets;

namespace ShipLean.Application.Http;

// Only globally routable unicast addresses pass. Refused: loopback, unspecified, private (10/8, 172.16/12, 192.168/16),
// carrier-grade NAT, link-local (169.254/16, fe80::/10), IPv6 unique-local (fc00::/7) and site-local, multicast, reserved,
// benchmarking, documentation (TEST-NET-1/2/3: 192.0.2/24, 198.51.100/24, 203.0.113/24) and the old 6to4 relay anycast (192.88.99/24). An IPv4 address wrapped in IPv6 as mapped (::ffff:0:0/96), NAT64 (64:ff9b::/96) or 6to4
// (2002::/16) is judged as the IPv4 address; IPv4-compatible (::/96) and the variable-length local-use NAT64 prefix
// (64:ff9b:1::/48) are refused outright.
public sealed class PublicAddressCheck : IAddressCheck
{
    public bool IsAllowed(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false,
        };
    }

    private static bool IsPublicV4(byte[] b) => !(
        b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
        || (b[0] == 100 && b[1] is >= 64 and <= 127)
        || (b[0] == 169 && b[1] == 254)
        || (b[0] == 172 && b[1] is >= 16 and <= 31)
        || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)
        || (b[0] == 192 && b[1] == 88 && b[2] == 99)
        || (b[0] == 198 && b[1] == 51 && b[2] == 100)
        || (b[0] == 203 && b[1] == 0 && b[2] == 113)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 198 && b[1] is 18 or 19));

    // IPv6 is an allow-list: only global unicast 2000::/3 passes, after an embedded IPv4 address (NAT64 64:ff9b::/96, 6to4
    // 2002::/16) is judged as IPv4. Inside 2000::/3 the protocol-assignment block (2001::/23, which holds
    // Teredo) and the documentation blocks (2001:db8::/32, 3fff::/20) are refused too.
    private static bool IsPublicV6(IPAddress address)
    {
        var b = address.GetAddressBytes();
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b)
        {
            return IsNat64(b);
        }
        if ((b[0] & 0xE0) != 0x20)
        {
            return false;
        }
        if (b[0] == 0x20 && b[1] == 0x02)
        {
            return IsPublicV4(b[2..6]);
        }
        return !((b[0] == 0x20 && b[1] == 0x01 && (b[2] & 0xFE) == 0x00)
                 || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8)
                 || (b[0] == 0x3f && b[1] == 0xff && (b[2] & 0xF0) == 0x00));
    }

    // 64:ff9b::/96 carries the IPv4 address in the last four bytes. Every other 64:ff9b::/32 address is refused: RFC 8215's
    // 64:ff9b:1::/48 may be used with prefix lengths from /32 to /96, so where the IPv4 address sits cannot be told.
    private static bool IsNat64(byte[] b)
        => b[4] == 0 && b[5] == 0 && b[6..12].All(x => x == 0) && IsPublicV4(b[12..]);
}
