using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Job;

// What the backup job logs in place of an exception whose message it can't vouch for: the type name and the stack trace,
// never the message or inner exceptions, which may hold a connection string or another secret. The exceptions whose
// contracts promise a secret-free message (and IOException, whose message names a path) are logged as they are.
public sealed class WithheldException : Exception
{
    private readonly string? _stackTrace;

    private WithheldException(Exception original)
        : base($"{original.GetType().FullName} (its message is withheld from the log: it may hold a secret)")
        => _stackTrace = original.StackTrace;

    public override string? StackTrace => _stackTrace;

    public static bool IsTrusted(Exception exception) => exception is DatabaseDumpException or BackupDestinationException or IOException;

    // The exception itself when its message is trusted, otherwise a WithheldException standing in for it.
    public static Exception Loggable(Exception exception) => IsTrusted(exception) ? exception : new WithheldException(exception);

    public override string ToString() => $"{GetType().FullName}: {Message}{Environment.NewLine}{StackTrace}";
}
