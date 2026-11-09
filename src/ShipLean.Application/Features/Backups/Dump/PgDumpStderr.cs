using System.Text;

namespace ShipLean.Application.Features.Backups.Dump;

// What of pg_dump's stderr is kept for a failure message: its own "pg_dump:" lines, the password scrubbed from the
// text after the prefix, 2 KB at most. When the limit is reached the tail is kept (the oldest text is dropped), because
// the last line is usually the real error.
public sealed class PgDumpStderr(string? password)
{
    public const int MaxBytes = 2048;
    private const string Prefix = "pg_dump:";
    private readonly StringBuilder _kept = new();

    public void Add(string line)
    {
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return;
        }
        var text = line[Prefix.Length..];
        var clean = Prefix + (string.IsNullOrEmpty(password) ? text : text.Replace(password, "***", StringComparison.Ordinal));
        if (_kept.Length > 0)
        {
            _kept.Append('\n');
        }
        _kept.Append(clean);
        if (_kept.Length > MaxBytes)
        {
            _kept.Remove(0, _kept.Length - MaxBytes);
        }
    }

    public override string ToString()
    {
        var text = _kept.ToString();
        var bytes = Encoding.UTF8.GetByteCount(text);
        var cut = 0;
        while (bytes > MaxBytes)
        {
            bytes -= Encoding.UTF8.GetByteCount(text.AsSpan(cut, 1));
            cut++;
        }
        return text[cut..];
    }
}
