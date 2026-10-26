using System.Text;

namespace ShipLean.WebApi.Tests.Import.Jira;

// Keeps what the host's console logger writes, to prove a secret never reached a log line. Never disposed: another host
// that started while it was Console.Out may still write to it.
internal sealed class LogCapture : TextWriter
{
    private readonly object _gate = new();
    private readonly StringBuilder _text = new();

    public override Encoding Encoding => Encoding.UTF8;

    public TextWriter Writer => this;

    public string Text
    {
        get
        {
            lock (_gate)
            {
                return _text.ToString();
            }
        }
    }

    public override void Write(char value)
    {
        lock (_gate)
        {
            _text.Append(value);
        }
    }

    public override void Write(string? value)
    {
        lock (_gate)
        {
            _text.Append(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_gate)
        {
            _text.Append(buffer, index, count);
        }
    }
}
