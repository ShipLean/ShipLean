using System.Text;
using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Inspection.Query;

// The one place text from outside becomes part of a store query (K3). Values go into TraceQL and LogQL as escaped, quoted
// strings; ClickHouse database and table names are validated and quoted; ClickHouse values always go as parameters.
public static partial class QueryText
{
    // A double-quoted TraceQL or LogQL string literal for the value: backslash and quote escaped, control characters as escapes.
    public static string TraceQlString(string value) => Quote(value);

    public static string LogQlString(string value) => Quote(value);

    // A ClickHouse identifier (database or table name), validated against ^[A-Za-z_][A-Za-z0-9_]*\z and backtick-quoted.
    public static string Identifier(string name)
    {
        if (!IdentifierPattern().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a valid ClickHouse identifier (letters, digits and underscore, not starting with a digit).", nameof(name));
        }
        return $"`{name}`";
    }

    private static string Quote(string value)
    {
        var text = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            text.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }
        return text.Append('"').ToString();
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*\\z")]
    private static partial Regex IdentifierPattern();
}
