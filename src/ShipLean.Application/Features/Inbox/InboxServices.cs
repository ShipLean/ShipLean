using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Inbox;

namespace ShipLean.Application.Features.Inbox;

// The Inbox's services. IInboxAskSource has one registration per source; the reader takes them all.
public sealed class InboxServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInboxReader, InboxReader>();
        services.AddScoped<IInboxAskSource, AskInboxSource>();
        services.AddScoped<IInboxAskSource, StatusLineInboxSource>();
        services.AddScoped<IInboxAskSource, MentionInboxSource>();
        services.AddScoped<IAnsweredReader, AnsweredReader>();
        services.AddScoped<IMentionRecorder, MentionRecorder>();
        services.AddScoped<IMentionWrites, MentionWrites>();
    }
}
