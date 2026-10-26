using System.Text;

namespace ShipLean.Core.People;

// The avatar's letters: the first letters of the name's first and last words ("Joel Dickson" -> "JD"). Words are split on
// any white space, a no-break space included. app_user.initials allows one or two uppercase A-Z letters; a name with none
// (digits only, say) gets "U".
public static class Initials
{
    public static string From(string name)
    {
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var letters = words.Select(FirstLetter).Where(c => c != '\0').ToArray();
        return letters.Length switch
        {
            0 => "U",
            1 => letters[0].ToString().ToUpperInvariant(),
            _ => $"{letters[0]}{letters[^1]}".ToUpperInvariant(),
        };
    }

    // The word's first character with any accent stripped ("Émile" -> 'E'; an emoji is a surrogate pair, so the first rune is taken, not the first char), or nothing when it isn't a letter A-Z.
    private static char FirstLetter(string word)
    {
        var first = word.EnumerateRunes().First().ToString().Normalize(NormalizationForm.FormD)[0];
        return first is >= 'A' and <= 'Z' or >= 'a' and <= 'z' ? first : '\0';
    }
}
