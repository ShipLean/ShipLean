namespace ShipLean.Contracts.Demos;

// How long a Feature's plan took, from its timeline in UTC (SHIP-2330): the execution, each wave in wave order, the longest waits
// between items (12 h or more, the timeline's own threshold) and the longest Blocked periods of its tasks, each list capped.
// TimelinePath is the Feature's timeline page in the app. A Feature cut to its header keeps Execution and TimelinePath only.
public sealed record SprintDemoTiming(
    SprintDemoExecution Execution,
    IReadOnlyList<SprintDemoWave> Waves,
    IReadOnlyList<SprintDemoWait> LongestWaits,
    IReadOnlyList<SprintDemoBlocked> LongestBlocked,
    string TimelinePath);
