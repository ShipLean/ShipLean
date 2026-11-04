using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Demos.YouTube;
using ShipLean.Application.Features.Holidays;
using ShipLean.Application.Features.Search.Index;

namespace ShipLean.LocalEnvironment.Hosting;

public static class HostProfiles
{
    // The hosted-service implementation types a profile removes from the app. Nothing leaves the machine in either
    // profile: no holiday feed (it would call the internet) and no webhook sending (a session's outbox stays queued).
    public static IReadOnlyList<Type> RemovedHostedServices(HostProfile profile) => profile switch
    {
        HostProfile.Test => [typeof(SearchIndexHostedService), typeof(HolidayRefreshService), typeof(AgentDeliveryService), typeof(AgentStaleService), typeof(DemoYouTubePublisher)],
        HostProfile.Local => [typeof(HolidayRefreshService), typeof(AgentDeliveryService), typeof(AgentStaleService), typeof(DemoYouTubePublisher)],
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown host profile."),
    };
}
