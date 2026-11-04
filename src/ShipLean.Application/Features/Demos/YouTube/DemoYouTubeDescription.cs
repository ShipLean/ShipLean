using System.Text;
using System.Text.Json;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

// What the uploaded video is called and what its description says, from the project, the sprint and the chapters file.
internal static class DemoYouTubeDescription
{
    // YouTube allows a title of 100 characters and a description of 5000 bytes, and refuses angle brackets in either.
    private const int MaxTitle = 100;
    private const int MaxDescriptionBytes = 4900;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Title(string projectName, string sprintName)
    {
        var title = Clean($"{projectName}: {sprintName} demo");
        return title.Length <= MaxTitle ? title : title[..MaxTitle];
    }

    // The chapters as "m:ss title" lines (h:mm:ss from an hour on, which is the form YouTube reads). A file that is missing or is not chapters gives a bare description.
    public static string Of(string? chaptersJson)
    {
        var chapters = Parse(chaptersJson);
        var lines = new StringBuilder();
        foreach (var chapter in chapters)
        {
            var line = $"{Clock(chapter.StartSeconds)} {Clean(chapter.Title)}";
            if (Encoding.UTF8.GetByteCount(lines.ToString()) + Encoding.UTF8.GetByteCount(line) + 1 > MaxDescriptionBytes)
            {
                break;
            }

            lines.Append(lines.Length == 0 ? string.Empty : "\n").Append(line);
        }

        return lines.ToString();
    }

    private static IReadOnlyList<DemoChapterJson> Parse(string? chaptersJson)
    {
        if (string.IsNullOrWhiteSpace(chaptersJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<DemoChaptersJson>(chaptersJson, Json)?.Chapters?.Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Title)).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Clock(decimal startSeconds)
    {
        var whole = (int)Math.Floor(Math.Max(0m, startSeconds));
        var time = TimeSpan.FromSeconds(whole);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    }

    private static string Clean(string text) => text.Replace("<", string.Empty, StringComparison.Ordinal).Replace(">", string.Empty, StringComparison.Ordinal).Trim();
}
