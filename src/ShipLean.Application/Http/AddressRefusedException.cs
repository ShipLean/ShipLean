using System.Net.Http;

namespace ShipLean.Application.Http;

// PinnedConnector refused to connect: the host resolved to an address the check does not allow (or to nothing). It is an
// HttpRequestException, so a caller that only catches those treats it as any failed request; one that must tell a private host
// from a down one looks for this type in the exception chain (SocketsHttpHandler wraps what ConnectCallback throws).
public sealed class AddressRefusedException(string message) : HttpRequestException(message)
{
}
