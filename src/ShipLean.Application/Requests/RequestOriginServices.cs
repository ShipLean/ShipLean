using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Requests;

public sealed class RequestOriginServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IRequestOrigin>(
            sp => new RequestOrigin(() => sp.GetRequiredService<IHttpContextAccessor>().HttpContext));
    }
}
