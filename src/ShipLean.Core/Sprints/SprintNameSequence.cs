namespace ShipLean.Core.Sprints;

// The names of sprints that don't exist yet, one after another: each starts the day after the previous one ends and lasts as
// long (the forecast's hypothetical sprints). Takes the existing names so none repeats.
public sealed class SprintNameSequence(SprintNamingConvention convention, IEnumerable<string> existing, DateOnly firstStart, int lengthDays, int firstNumber)
{
    private readonly HashSet<string> _taken = [.. existing];
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
