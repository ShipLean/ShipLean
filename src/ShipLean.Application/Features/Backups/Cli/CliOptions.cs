namespace ShipLean.Application.Features.Backups.Cli;

// The options of a restore verb: --in <path>, --out <path> (decrypt-backup only) and --overwrite (restore-blobs only).
internal sealed record CliOptions(string? In, string? Out, bool Overwrite)
{
    // Error is the usage message when the arguments are wrong; Options is null then.
    public static (CliOptions? Options, string? Error) Parse(IReadOnlyList<string> args, bool takesOut, bool takesOverwrite)
    {
        string? input = null;
        string? output = null;
        var overwrite = false;
        var rest = new Queue<string>(args.Skip(1));
        while (rest.TryDequeue(out var arg))
        {
            if (arg == "--in" && input is null && rest.TryDequeue(out var inValue))
            {
                input = inValue;
            }
            else if (takesOut && arg == "--out" && output is null && rest.TryDequeue(out var outValue))
            {
                output = outValue;
            }
            else if (takesOverwrite && arg == "--overwrite")
            {
                overwrite = true;
            }
            else
            {
                return (null, $"Unexpected or incomplete argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return (null, "--in <path> is required.");
        }

        if (takesOut && string.IsNullOrWhiteSpace(output))
        {
            return (null, "--out <path> is required.");
        }

        return (new CliOptions(input, output, overwrite), null);
    }
}
