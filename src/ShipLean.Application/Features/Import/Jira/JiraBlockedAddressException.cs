namespace ShipLean.Application.Features.Import.Jira;

// A Jira host resolved to a private, loopback or link-local address. Raised while connecting, so it arrives wrapped in an HttpRequestException.
public sealed class JiraBlockedAddressException(string host) : Exception($"{host} resolves to a private or local address.")
{
}
