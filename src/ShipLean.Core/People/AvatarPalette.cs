namespace ShipLean.Core.People;

// The design's identity colours (design/screens/profile.html), the same in both themes: the only colours a user can pick.
// A new user gets one by a stable hash of the email, so it never changes between runs or machines.
public static class AvatarPalette
{
    public static readonly IReadOnlyList<string> All =
        ["#2f7fd8", "#16a089", "#7b4fc9", "#c9611a", "#b83d7a", "#3f7d5a", "#5a5fc9", "#8a5a2b", "#a3416b", "#52627c"];

    public static string For(string email)
    {
        // FNV-1a over the lowercased email.
        var hash = 2166136261u;
        foreach (var c in email.ToLowerInvariant())
        {
            hash = (hash ^ c) * 16777619u;
        }

        return All[(int)(hash % (uint)All.Count)];
    }
}
