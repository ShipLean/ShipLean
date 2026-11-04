using System.Text.Json;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Upload;

// K8's chapters part: the rules a chapters.json must meet, as a pure parse. The live-item check on featureKey needs the database and is the upload's.
public static class DemoChaptersFile
{
    public const int MaxBytes = 1024 * 1024;
    public const int MaxChapters = 200;
    public const int MaxTitleLength = 200;
    public const string Part = "chapters";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static DemoChaptersJson Parse(ReadOnlySpan<byte> json)
    {
        DemoChaptersJson? file;
        try
        {
            file = JsonSerializer.Deserialize<DemoChaptersJson>(json, Options);
        }
        catch (JsonException)
        {
            throw Invalid("The chapters part must be JSON like {\"durationSeconds\": 142.6, \"chapters\": [{\"title\": \"Intro\", \"startSeconds\": 0}]}.");
        }

        if (file is null || file.Chapters is null)
        {
            throw Invalid("The chapters part must hold durationSeconds and a chapters list.");
        }

        if (file.DurationSeconds <= 0)
        {
            throw Invalid("The chapters' durationSeconds must be above 0.");
        }

        if (file.Chapters.Count is 0 or > MaxChapters)
        {
            throw Invalid($"The chapters part must hold 1 to {MaxChapters} chapters.");
        }

        for (var i = 0; i < file.Chapters.Count; i++)
        {
            Check(file, i);
        }

        return file;
    }

    private static void Check(DemoChaptersJson file, int index)
    {
        var chapter = file.Chapters[index];
        var number = index + 1;
        if (chapter is null || string.IsNullOrWhiteSpace(chapter.Title) || chapter.Title.Length > MaxTitleLength)
        {
            throw Invalid($"Chapter {number} needs a title of 1 to {MaxTitleLength} characters.");
        }

        if (chapter.Title.Any(char.IsControl))
        {
            throw Invalid($"Chapter {number}'s title holds a control character.");
        }

        if (chapter.FeatureKey is { } key && string.IsNullOrWhiteSpace(key))
        {
            throw Invalid($"Chapter {number}'s featureKey is blank.");
        }

        if (index == 0 && chapter.StartSeconds != 0)
        {
            throw Invalid("The first chapter must start at 0.");
        }

        if (index > 0 && chapter.StartSeconds <= file.Chapters[index - 1].StartSeconds)
        {
            throw Invalid($"Chapter {number} must start after chapter {index}.");
        }

        if (chapter.StartSeconds >= file.DurationSeconds)
        {
            throw Invalid($"Chapter {number} must start before the video ends ({file.DurationSeconds} seconds).");
        }
    }

    private static ApiException Invalid(string message) => ApiException.Validation(message, Part);
}
