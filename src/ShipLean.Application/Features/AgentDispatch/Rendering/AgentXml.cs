using System.Text;

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

internal static class AgentXml
{
    // Escapes the five XML specials and drops characters XML 1.0 cannot carry (control characters, lone surrogates).
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var builder = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\'': builder.Append("&apos;"); break;
                case '\t' or '\n' or '\r':
                    builder.Append(rune);
                    break;
                case >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 when rune != Rune.ReplacementChar:
                    builder.Append(rune);
                    break;
            }
        }
        return builder.ToString();
    }
}
