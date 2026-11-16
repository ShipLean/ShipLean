using ShipLean.Contracts;

namespace ShipLean.WebApi.Infrastructure;

// The framework's IHttpContextAccessor, which only CurrentRequest takes (AG0009). CurrentRequest registers itself as
// ICurrentRequest through its Agoda.IoC attribute.
public sealed class CurrentRequestServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddHttpContextAccessor();
}
