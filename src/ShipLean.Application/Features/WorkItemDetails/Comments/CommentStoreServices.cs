using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;

namespace ShipLean.Application.Features.WorkItemDetails.Comments;

public sealed class CommentStoreServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<ICommentStore, CommentStore>();
}
