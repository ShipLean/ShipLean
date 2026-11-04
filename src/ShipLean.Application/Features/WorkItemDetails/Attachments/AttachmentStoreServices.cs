using ShipLean.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

public sealed class AttachmentStoreServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Read from DI's configuration when first resolved, after the host applied its overrides; AttachmentLimitsStartup resolves it at start so a bad setting stops startup.
        services.AddSingleton(sp => AttachmentLimits.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
        services.AddHostedService<AttachmentLimitsStartup>();
        services.AddScoped<AttachmentStore>();
        services.AddScoped<IAttachmentStore>(sp => sp.GetRequiredService<AttachmentStore>());
    }
}
