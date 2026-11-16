using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Reviews;

namespace ShipLean.Application.Features.Reviews;

public sealed class ReviewServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IReviewLog, ReviewLog>();
}
