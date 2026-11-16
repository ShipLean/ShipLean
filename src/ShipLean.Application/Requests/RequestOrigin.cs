using ShipLean.Contracts;

namespace ShipLean.Application.Requests;

// The ClientHeader of the current request. Blank counts as absent.
public sealed class RequestOrigin(ICurrentRequest request) : IRequestOrigin
{
    public string? ClientId
    {
        get
        {
            var value = request.Header(ClientHeader.Name);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
