using System.Globalization;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.LocalHost;

public static class LocalHostOptionsParser
{
    public static LocalHostParseResult Parse(IReadOnlyList<string> args)
    {
        var options = new LocalHostOptions();
        var remaining = new Queue<string>(args);
        while (remaining.TryDequeue(out var flag))
        {
            if (flag == "--redis")
            {
                options = options with { Redis = true };
                continue;
            }
            if (flag is not ("--port" or "--data" or "--database"))
            {
                return Fail($"Unknown option '{flag}'.");
            }
            if (!remaining.TryDequeue(out var value))
            {
                return Fail($"{flag} needs a value.");
            }
            var (updated, error) = Apply(options, flag, value);
            if (error != null)
            {
                return Fail(error);
            }
            options = updated;
        }
        return new LocalHostParseResult(options, null);
    }

    private static (LocalHostOptions Options, string? Error) Apply(LocalHostOptions options, string flag, string value)
    {
        switch (flag)
        {
            case "--port":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port <= 65535
                    ? (options with { Port = port }, null)
                    : (options, $"--port must be a number from 0 to 65535, not '{value}'.");
            case "--data":
                return ParseData(value) is { } data
                    ? (options with { Data = data }, null)
                    : (options, $"--data must be fresh, keep or empty, not '{value}'.");
            default:
                return ShipLeanDatabase.IsValidName(value)
                    ? (options with { Database = value }, null)
                    : (options, $"--database '{value}' is not a valid name: use shiplean_ then lowercase letters, digits and _.");
        }
    }

    // Only the three words: Enum.TryParse would also take "1" and "fresh,keep".
    private static LocalHostData? ParseData(string value)
    {
        if (string.Equals(value, "fresh", StringComparison.OrdinalIgnoreCase))
        {
            return LocalHostData.Fresh;
        }
        if (string.Equals(value, "keep", StringComparison.OrdinalIgnoreCase))
        {
            return LocalHostData.Keep;
        }
        return string.Equals(value, "empty", StringComparison.OrdinalIgnoreCase) ? LocalHostData.Empty : null;
    }

    private static LocalHostParseResult Fail(string error) => new(null, error);
}
