using Microsoft.AspNetCore.Http.Connections;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Realtime;

public sealed class RealtimeEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapHub<ProjectHub>(ProjectHub.Path, options =>
            {
                // WebSockets only: a long-polling connection token is not tied to its user and lands in proxy logs.
                options.Transports = HttpTransportType.WebSockets;
                // A socket ends when the cookie it was opened with expires.
                options.CloseOnAuthenticationExpiration = true;
            })
            .RequireAuthorization(RealtimeServices.HubPolicy);
}
