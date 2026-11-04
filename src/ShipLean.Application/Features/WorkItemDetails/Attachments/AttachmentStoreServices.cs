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
        // Resolved now so a bad setting stops startup; the instance is the one everything shares.
        services.AddSingleton(AttachmentLimits.FromConfiguration(configuration));
        services.AddScoped<AttachmentStore>();
        services.AddScoped<IAttachmentStore>(sp => sp.GetRequiredService<AttachmentStore>());
    }
}
