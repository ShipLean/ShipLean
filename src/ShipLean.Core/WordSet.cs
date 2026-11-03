namespace ShipLean.Core;

// The distinct words of a title as WordOverlap counts them, prepared once so many titles can be compared without splitting again.
public sealed class WordSet
{
    private readonly HashSet<string> _words;

    internal WordSet(IEnumerable<string> words) => _words = new HashSet<string>(words, StringComparer.Ordinal);

    public int Count => _words.Count;

    // How many words the two sets share.
    public int Shared(WordSet other)
    {
        var (small, large) = Count <= other.Count ? (_words, other._words) : (other._words, _words);
        return small.Count(large.Contains);
    }
}
