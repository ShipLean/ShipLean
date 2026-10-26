using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

public sealed class AttachmentStoreServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new AttachmentLimits(
                long.TryParse(config[AttachmentLimits.ConfigKey], out var max) && max > 0 ? max : AttachmentLimits.DefaultMaxBytes,
                long.TryParse(config[AttachmentLimits.ItemConfigKey], out var itemMax) && itemMax > 0 ? itemMax : AttachmentLimits.DefaultMaxItemBytes);
        });
        services.AddScoped<AttachmentStore>();
        services.AddScoped<IAttachmentStore>(sp => sp.GetRequiredService<AttachmentStore>());
    }
}
