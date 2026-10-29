namespace ShipLean.Core.Sprints;

// The names of sprints that don't exist yet, one after another: each starts the day after the previous one ends and lasts as
// long (the forecast's hypothetical sprints). Takes the existing names so none repeats.
public sealed class SprintNameSequence(SprintNamingConvention convention, IEnumerable<string> existing, DateOnly firstStart, int lengthDays, int firstNumber)
{
    // Case-insensitive, like the sprint name uniqueness in the database (lower(name)).
    private readonly HashSet<string> _taken = new(existing, StringComparer.OrdinalIgnoreCase);
    private DateOnly _start = firstStart;
    private int _number = firstNumber;

    public string Next()
    {
        var name = SprintNaming.Name(convention, _start, lengthDays, _taken.Contains, _number);
        _taken.Add(name);
        _start = _start.AddDays(Math.Max(1, lengthDays));
        _number++;
        return name;
    }
}
