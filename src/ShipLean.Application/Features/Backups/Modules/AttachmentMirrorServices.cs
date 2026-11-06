using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class AttachmentMirrorServices : IServiceModule
{
    // TODO: the fake (copies nothing) stands in for the attachment mirror, which replaces it here.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IAttachmentMirror>(new FakeAttachmentMirror());
}
