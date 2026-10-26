using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Features.Users;

public sealed class UserDirectoryServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IUserDirectory, UserDirectory>();
}
