using System.Text;

namespace ShipLean.Application.Features.Backups.Dump;

// What of pg_dump's stderr is kept for a failure message: its own "pg_dump:" lines, the password scrubbed, 2 KB at most.
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
        var clean = string.IsNullOrEmpty(password) ? line : line.Replace(password, "***", StringComparison.Ordinal);
        if (_kept.Length > 0)
        {
            _kept.Append('\n');
        }
        _kept.Append(clean);
        if (Encoding.UTF8.GetByteCount(_kept.ToString()) > MaxBytes)
        {
            Trim();
        }
    }

    public override string ToString() => _kept.ToString();

    private void Trim()
    {
        var text = _kept.ToString();
        while (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            text = text[..^1];
        }
        _kept.Clear().Append(text);
    }
}
