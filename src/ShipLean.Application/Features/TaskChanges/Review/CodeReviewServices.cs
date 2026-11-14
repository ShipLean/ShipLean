using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Review;

public sealed class CodeReviewServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ICodeReview, StubCodeReview>();
}
