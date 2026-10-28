namespace ShipLean.Application.Features.AgentDispatch;

// Linear's retry schedule for a webhook: 1 minute, 1 hour, 6 hours. A `created` that fails twice fails the session without waiting
// for the longer ones (the item is waiting on it); a `prompted` keeps the whole schedule.
internal static class AgentDeliverySchedule
{
    private static readonly TimeSpan[] Created = [TimeSpan.FromMinutes(1)];
    private static readonly TimeSpan[] Prompted = [TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), TimeSpan.FromHours(6)];

    // The wait before the next try after attemptsMade failed tries; null when the schedule is used up.
    public static TimeSpan? RetryAfter(string action, int attemptsMade)
    {
        var schedule = action == "created" ? Created : Prompted;
        return attemptsMade >= 1 && attemptsMade <= schedule.Length ? schedule[attemptsMade - 1] : null;
    }
}
