using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Import.Jira;

// Which Jira addresses ShipLean will talk to. A Jira base URL is typed in by a project admin, so without this rule the
// import is a way to make the server call its own network. Private, loopback and link-local addresses are refused
// unless SHIPLEAN_JIRA_ALLOW_LOCAL_ENDPOINTS is true (read at call time, so a test host can turn it on).
public static class JiraEndpointPolicy
{
    public const string AllowLocalKey = "SHIPLEAN_JIRA_ALLOW_LOCAL_ENDPOINTS";

    private const int FirstMulticastOctet = 224;
    private const int CarrierGradeOctet = 100;
    private const int BenchmarkOctet = 198;

    public static bool AllowsLocal(IConfiguration configuration) => configuration.GetValue(AllowLocalKey, false);

    // The address without query, fragment or trailing "/", or ApiException.Validation. Only https, or http when local
    // endpoints are allowed (a Jira on a developer machine); never credentials in the URL. A host name is checked by
    // IsLocalName and, when the connection is opened, by the addresses it resolves to (JiraConnector).
    public static Uri Parse(string? baseUrl, bool allowLocal)
    {
        var text = (baseUrl ?? string.Empty).Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || !(uri.Scheme == Uri.UriSchemeHttps || (allowLocal && uri.Scheme == Uri.UriSchemeHttp)))
        {
            throw ApiException.Validation("The Jira address must be an https address such as https://your-team.atlassian.net, with no credentials, query or fragment.", "baseUrl");
        }
        if (!allowLocal && IsLocalHost(uri))
        {
            throw ApiException.Validation("That Jira address is on a private or local network, which ShipLean does not connect to.", "baseUrl");
        }
        return new Uri(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
    }

    public static bool IsLocalHost(Uri uri)
        => uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            ? IPAddress.TryParse(uri.IdnHost, out var address) && IsLocal(address)
            : IsLocalName(uri.IdnHost);

    public static bool IsLocalName(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    public static bool IsLocal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return IsLocalV4(address.GetAddressBytes());
        }
        return IsLocalV6(address) || (EmbeddedV4(address.GetAddressBytes()) is { } inner && IsLocal(inner));
    }

    // The IPv4 address an IPv6 one carries or translates to: NAT64 (64:ff9b::/96), 6to4 (2002::/16) and the deprecated IPv4-compatible form (::a.b.c.d).
    // A host that reaches 10.0.0.1 as 64:ff9b::a00:1 is as local as 10.0.0.1.
    private static IPAddress? EmbeddedV4(byte[] octets) => octets switch
    {
        [0x00, 0x64, 0xFF, 0x9B, 0, 0, 0, 0, 0, 0, 0, 0, ..] => new IPAddress(octets[12..16]),
        [0x20, 0x02, ..] => new IPAddress(octets[2..6]),
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ..] => new IPAddress(octets[12..16]),
        _ => null,
    };

    private static bool IsLocalV4(byte[] octets) => octets switch
    {
        [0, ..] => true,
        [10, ..] => true,
        [127, ..] => true,
        [169, 254, ..] => true,
        [172, >= 16 and <= 31, ..] => true,
        [192, 168, ..] => true,
        [CarrierGradeOctet, >= 64 and <= 127, ..] => true,
        [BenchmarkOctet, 18 or 19, ..] => true,
        [>= FirstMulticastOctet, ..] => true,
        _ => false,
    };

    // Prefixes that translate to or tunnel IPv4 and are not unwrapped: local-use NAT64 (64:ff9b:1::/48), SIIT (::ffff:0:0/96) and Teredo (2001::/32).
    private static bool IsTranslationPrefix(byte[] octets) => octets switch
    {
        [0x00, 0x64, 0xFF, 0x9B, 0x00, 0x01, ..] => true,
        [0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0, 0, ..] => true,
        [0x20, 0x01, 0x00, 0x00, ..] => true,
        _ => false,
    };

    private static bool IsLocalV6(IPAddress address)
    {
        var octets = address.GetAddressBytes();
        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || (octets[0] & 0xFE) == 0xFC || IsTranslationPrefix(octets);
    }
}
