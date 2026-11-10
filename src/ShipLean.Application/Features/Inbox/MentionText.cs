namespace ShipLean.Application.Features.Inbox;

// Finds the members a comment body @mentions by full name: case-insensitive, longest name first, at the start of a line or after
// whitespace, ending at the end, whitespace or .,;:!?) and never inside code. excludeUserId (the author) is never returned.
public static class MentionText
{
    // TODO: match "@Full Name" mentions in body against members; until then nothing is mentioned.
    public static IReadOnlyList<Guid> Find(string body, IReadOnlyList<MentionCandidate> members, Guid? excludeUserId) => [];
}
