using System.Text;
using System.Text.Json;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Prompt;

public sealed class SprintDemoPrompt : ISprintDemoPrompt
{
    private const string Resource = "sprint-demo/sprint-demo-prompt.md";

    // TODO: the real prompt replaces this: only the placeholders of the embedded file are filled.
    public string Render(SprintDemoData data, DemoUploadTarget upload, DemoBrand brand)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["upload_url"] = upload.Url,
            ["upload_expires"] = upload.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["max_video_mb"] = (upload.MaxVideoBytes / (1024 * 1024)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["curl_command"] = $"curl --fail-with-body -F chapters=@chapters.json -F script=@script.md -F video=@demo.mp4 '{upload.Url}'",
            ["demo_item"] = upload.ItemKey,
            ["sprint_name"] = upload.SprintName,
            ["logo_block"] = brand.LogoUrl ?? brand.LogoSvg ?? string.Empty,
            ["tokens_css"] = brand.TokensCss,
            ["sprint_data_json"] = JsonSerializer.Serialize(data),
        };
        var text = new StringBuilder(Template());
        foreach (var (name, value) in values)
        {
            text.Replace("{{" + name + "}}", value);
        }
        return text.ToString();
    }

    private static string Template()
    {
        using var stream = typeof(SprintDemoPrompt).Assembly.GetManifestResourceStream(Resource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
