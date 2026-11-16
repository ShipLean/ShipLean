using System.Text;

namespace ShipLean.Application.Features.Mcp.Agents;

// The cap on a text field of an agent session in an MCP result: at most MaxBytes of UTF-8, cut between characters (never inside a
// multi-byte character or a surrogate pair), so the result is always valid text.
public static class AgentTextCap
{
    public const int MaxBytes = 4096;

    public static string? Cap(string? text, ref bool truncated)
    {
        if (text is null || Encoding.UTF8.GetByteCount(text) <= MaxBytes)
        {
            return text;
        }

        var bytes = 0;
        var end = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;
            if (bytes > MaxBytes)
            {
                break;
            }

            end += rune.Utf16SequenceLength;
        }

        truncated = true;
        return text[..end];
    }
}
