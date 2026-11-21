using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// A prompt that is just the upload link, so a test can read the token out of the delivery. Throws while Fail is set.
internal sealed class UploadUrlPrompt : ISprintDemoPrompt
{
    public volatile bool Fail;

    // The brand the last render was given.
    public volatile DemoBrand? LastBrand;

    // The channel the last render was given (null before any render).
    public DemoPromptChannel? LastChannel { get; private set; }

    public string Render(SprintDemoData data, DemoUploadTarget upload, DemoBrand brand, DemoPromptChannel channel, string template)
    {
        LastBrand = brand;
        LastChannel = channel;
        return Fail ? throw new InvalidOperationException("The prompt failed.") : $"Upload to {upload.Url}";
    }
}
