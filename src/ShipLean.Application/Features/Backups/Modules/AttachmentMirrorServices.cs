using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Mirror;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class AttachmentMirrorServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IAttachmentMirror, AttachmentMirror>();
}
