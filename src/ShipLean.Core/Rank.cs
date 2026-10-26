namespace ShipLean.Core;

// Sortable order keys for backlog and column order: strings of digits that compare by ordinal (COLLATE "C") order and
// read as the digits after a decimal point, so a key always fits between two neighbours. Between never returns a key
// ending in '0' (nothing could then sort before it), and the seed's zero-padded numbers are valid neighbours: trailing
// zeros do not change a key's value, so they are trimmed from both neighbours before the middle is found.
public static class Rank
{
    private const int RADIX = 10;

    // A key that sorts after before and before after (ordinally). Null before = the front; null after = the back; both null = a middle key.
    // Throws ArgumentException when a neighbour is not made of digits, before does not sort ahead of after or after is zero, and
    // InvalidOperationException when the neighbours are the same fraction ("5" and "50"), which leaves no room.
    public static string Between(string? before, string? after)
    {
        var low = before ?? string.Empty;
        Require(low);
        if (after is not null)
        {
            Require(after);
            if (string.CompareOrdinal(low, after) >= 0)
            {
                throw new ArgumentException("The key before must sort ahead of the key after.");
            }
        }

        var trimmedAfter = after?.TrimEnd('0');
        if (trimmedAfter is { Length: 0 })
        {
            throw new ArgumentException("Nothing sorts before a key whose value is zero.");
        }

        return Midpoint(low.TrimEnd('0'), trimmedAfter);
    }

    private static string Midpoint(string low, string? high)
    {
        if (high is not null)
        {
            var common = 0;
            while (common < high.Length && DigitAt(low, common) == high[common] - '0')
            {
                common++;
            }

            if (common == high.Length)
            {
                throw new InvalidOperationException("There is no key between two keys that are the same fraction.");
            }

            if (common > 0)
            {
                return high[..common] + Midpoint(low.Length > common ? low[common..] : string.Empty, high[common..]);
            }
        }

        var digitLow = DigitAt(low, 0);
        var digitHigh = high is null ? RADIX : high[0] - '0';
        if (digitHigh - digitLow > 1)
        {
            return ((digitLow + digitHigh + 1) / 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (high is { Length: > 1 })
        {
            return high[..1];
        }

        // high is null or one digit, and low's first digit is the one before it: keep that digit and find room after the rest of low.
        return digitLow.ToString(System.Globalization.CultureInfo.InvariantCulture) + Midpoint(low.Length > 1 ? low[1..] : string.Empty, null);
    }

    private static int DigitAt(string key, int index) => index < key.Length ? key[index] - '0' : 0;

    private static void Require(string key)
    {
        if (key.Any(c => c is < '0' or > '9'))
        {
            throw new ArgumentException($"A rank is made of digits only: '{key}'.");
        }
    }
}
