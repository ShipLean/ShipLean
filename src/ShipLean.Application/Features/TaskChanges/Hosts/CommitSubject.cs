namespace ShipLean.Application.Features.TaskChanges.Hosts;

// A commit's subject as stored: its first line, cut to MaxLength without ending on half of a surrogate pair. Same limit as
// the Links sources' titles, kept here because one feature never references another (ArchitectureTests).
public static class CommitSubject
{
    public const int MaxLength = 255;

    public static string Cut(string? text)
    {
        if (text is null)
        {
            return "";
        }

        if (text.Length <= MaxLength)
        {
            return text;
        }

        return text[..(char.IsHighSurrogate(text[MaxLength - 1]) ? MaxLength - 1 : MaxLength)];
    }
}
