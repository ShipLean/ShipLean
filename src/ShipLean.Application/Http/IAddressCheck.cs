using System.Net;

namespace ShipLean.Application.Http;

// Whether the metadata-document fetcher may connect to an address. Swappable so tests can drive the guard.
public interface IAddressCheck
{
    bool IsAllowed(IPAddress address);
}
