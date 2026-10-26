using System.Globalization;

namespace ShipLean.TestSupport.Database;

// What a template database is named after: the hash of the db/ scripts and the UTC run date. A changed script or a new
// day means a new template, built once; templates dated before today are dropped.
public static class TemplateKey
{
    public const string DateFormat = "yyyyMMdd";

    public static string For(string scriptsHash, DateOnly runDate)
        => $"{scriptsHash}_{runDate.ToString(DateFormat, CultureInfo.InvariantCulture)}";

    // The date a template name carries, or null when the name is not a template of this form.
    public static DateOnly? DateOf(string templateName)
    {
        var tail = templateName[(templateName.LastIndexOf('_') + 1)..];
        return DateOnly.TryParseExact(tail, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
