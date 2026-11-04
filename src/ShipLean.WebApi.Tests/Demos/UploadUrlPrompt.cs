using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// A prompt that is just the upload link, so a test can read the token out of the delivery. Throws while Fail is set.
internal sealed class UploadUrlPrompt : ISprintDemoPrompt
{
    public volatile bool Fail;

    public string Render(SprintDemoData data, DemoUploadTarget upload, DemoBrand brand)
        => Fail ? throw new InvalidOperationException("The prompt failed.") : $"Upload to {upload.Url}";
}
