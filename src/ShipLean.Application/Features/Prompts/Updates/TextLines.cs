namespace ShipLean.Application.Features.Prompts.Updates;

/// <summary>Splits text into lines that keep their own "\n", so the last line's missing newline is part of that line.</summary>
internal static class TextLines
{
    public static IReadOnlyList<string> Split(string text)
    {
        var lf = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = new List<string>();
        var start = 0;
        while (start < lf.Length)
        {
            var newline = lf.IndexOf('\n', start);
            var end = newline < 0 ? lf.Length : newline + 1;
            lines.Add(lf[start..end]);
            start = end;
        }

        return lines;
    }
}
