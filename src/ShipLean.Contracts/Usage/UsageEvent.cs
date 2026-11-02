namespace ShipLean.Contracts.Usage;

// One thing somebody did. Only ids, catalogue names, enum-like strings, numbers and booleans: never free text.
// The constructor refuses a name outside UsageEventNames.All, a property key outside UsageProps.All, and a value that is not
// a string (200 characters at most), long, int, double or bool. That is a programming error, so it throws ArgumentException.
public sealed record UsageEvent
{
    public const int MaxStringLength = 200;

    public UsageEvent(
        string name,
        UsageSurface surface,
        UsageActor actor,
        Guid? projectId,
        int projectCount,
        UsageOutcome outcome,
        string? errorCode,
        long? durationMs,
        string source,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, object> properties)
    {
        if (!UsageEventNames.All.Contains(name))
        {
            throw new ArgumentException($"'{name}' is not a usage event name.", nameof(name));
        }
        foreach (var (key, value) in properties)
        {
            if (!UsageProps.All.Contains(key))
            {
                throw new ArgumentException($"'{key}' is not a usage property key.", nameof(properties));
            }
            if (value is string text ? text.Length > MaxStringLength : value is not (long or int or double or bool))
            {
                throw new ArgumentException($"Property '{key}' must be a string of at most {MaxStringLength} characters, a long, int, double or bool.", nameof(properties));
            }
        }

        Name = name;
        Surface = surface;
        Actor = actor;
        ProjectId = projectId;
        ProjectCount = projectCount;
        Outcome = outcome;
        ErrorCode = errorCode;
        DurationMs = durationMs;
        Source = source;
        OccurredAt = occurredAt;
        Properties = properties;
    }

    public string Name { get; }

    public UsageSurface Surface { get; }

    public UsageActor Actor { get; }

    // Only when exactly one project was touched.
    public Guid? ProjectId { get; }

    public int ProjectCount { get; }

    public UsageOutcome Outcome { get; }

    // ApiException.Code when Outcome is Error, else null.
    public string? ErrorCode { get; }

    public long? DurationMs { get; }

    // "tool:<name>" | "api:<Controller>.<Action>" | "screen" | "system:<Class>"
    public string Source { get; }

    public DateTimeOffset OccurredAt { get; }

    public IReadOnlyDictionary<string, object> Properties { get; }
}
