using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Questions;

public sealed class QuestionWritesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IQuestionWrites, QuestionWrites>();
}
