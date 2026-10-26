using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Mcp.Planning.Waves;

// A wave's label, such as "0.1", "1" or "2": dotted whole numbers, compared part by part as numbers, so 0.2 < 1 < 1.1 < 2
// and 1.10 > 1.9. A label is a prefix of its sub-waves and sorts before them (1 < 1.0 < 1.1). Text is the canonical form,
// leading zeros dropped ("01.1" is "1.1"), and two labels are equal when their texts are.
public sealed partial class WaveLabel : IComparable<WaveLabel>, IEquatable<WaveLabel>
{
    private readonly int[] _parts;

    private WaveLabel(int[] parts)
    {
        _parts = parts;
        Text = string.Join('.', parts.Select(part => part.ToString(CultureInfo.InvariantCulture)));
    }

    public string Text { get; }

    public static bool TryParse(string? text, [NotNullWhen(true)] out WaveLabel? label)
    {
        label = null;
        if (text is null || !Dotted().IsMatch(text))
        {
            return false;
        }

        var parts = new List<int>();
        foreach (var part in text.Split('.'))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }
            parts.Add(number);
        }

        label = new WaveLabel([.. parts]);
        return true;
    }

    // For labels known to be good (fixtures, tests); input from an agent goes through TryParse.
    public static WaveLabel Parse(string text)
        => TryParse(text, out var label) ? label : throw new FormatException($"\"{text}\" is not a wave label such as 0.1, 1 or 2.");

    // The whole wave after this one and all its sub-waves: 1 after 0.2, 3 after 2.1.
    public WaveLabel NextWhole() => new([_parts[0] + 1]);

    public int CompareTo(WaveLabel? other)
    {
        if (other is null)
        {
            return 1;
        }

        for (var i = 0; i < Math.Min(_parts.Length, other._parts.Length); i++)
        {
            var compared = _parts[i].CompareTo(other._parts[i]);
            if (compared != 0)
            {
                return compared;
            }
        }
        return _parts.Length.CompareTo(other._parts.Length);
    }

    public bool Equals(WaveLabel? other) => other is not null && Text == other.Text;

    public override bool Equals(object? obj) => obj is WaveLabel other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Text);

    public override string ToString() => Text;

    public static bool operator ==(WaveLabel? left, WaveLabel? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(WaveLabel? left, WaveLabel? right) => !(left == right);

    public static bool operator <(WaveLabel left, WaveLabel right) => left.CompareTo(right) < 0;

    public static bool operator >(WaveLabel left, WaveLabel right) => left.CompareTo(right) > 0;

    public static bool operator <=(WaveLabel left, WaveLabel right) => left.CompareTo(right) <= 0;

    public static bool operator >=(WaveLabel left, WaveLabel right) => left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^[0-9]{1,9}(\.[0-9]{1,9})*\z")]
    private static partial Regex Dotted();
}
