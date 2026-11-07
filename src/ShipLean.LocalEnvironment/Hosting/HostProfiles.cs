using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Backups.Schedule;
using ShipLean.Application.Features.Demos.YouTube;
using ShipLean.Application.Features.Holidays;
using ShipLean.Application.Features.Search.Index;

namespace ShipLean.LocalEnvironment.Hosting;

public static class HostProfiles
{
    // The hosted-service implementation types a profile removes from the app. Nothing leaves the machine in either
    // profile: no holiday feed (it would call the internet), no webhook sending (a session's outbox stays queued) and
    // no scheduled backups (Back up now still runs: the backup worker stays).
    public static IReadOnlyList<Type> RemovedHostedServices(HostProfile profile) => profile switch
    {
        HostProfile.Test => [typeof(SearchIndexHostedService), typeof(HolidayRefreshService), typeof(AgentDeliveryService), typeof(AgentStaleService), typeof(DemoYouTubePublisher), typeof(BackupScheduleService)],
        HostProfile.Local => [typeof(HolidayRefreshService), typeof(AgentDeliveryService), typeof(AgentStaleService), typeof(DemoYouTubePublisher), typeof(BackupScheduleService)],
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown host profile."),
    };
}
