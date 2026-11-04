using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Demos;

// A project's Demos settings tab: the Automatic switch and environment text with their own Save, the logo and tokens panel and the YouTube panel.
// The instance-level YouTube client fields are on the instance Integrations tab and are here too, because a journey sets them up first.
public sealed class DemoSettingsTab(IPage page)
{
    public ILocator Tab => page.GetByTestId(DemosTestIds.SettingsTabDemos);

    public ILocator Panel => page.GetByTestId(DemosTestIds.DemoSettingsPanel);

    public ILocator Automatic => page.GetByTestId(DemosTestIds.DemoSettingsAutomatic);

    public ILocator AutomaticBlocked => page.GetByTestId(DemosTestIds.DemoSettingsAutomaticBlocked);

    public ILocator Environment => page.GetByTestId(DemosTestIds.DemoSettingsEnvironment);

    public ILocator Save => page.GetByTestId(DemosTestIds.DemoSettingsSave);

    public ILocator BrandPanel => page.GetByTestId(DemosTestIds.DemoBrandPanel);

    public ILocator LogoPreview => page.GetByTestId(DemosTestIds.DemoLogoPreview);

    public ILocator LogoDefault => page.GetByTestId(DemosTestIds.DemoLogoDefault);

    public ILocator LogoFile => page.GetByTestId(DemosTestIds.DemoLogoFile);

    public ILocator LogoRemove => page.GetByTestId(DemosTestIds.DemoLogoRemove);

    public ILocator LogoError => page.GetByTestId(DemosTestIds.DemoLogoError);

    public ILocator Tokens => page.GetByTestId(DemosTestIds.DemoTokens);

    public ILocator TokensSave => page.GetByTestId(DemosTestIds.DemoTokensSave);

    public ILocator TokensError => page.GetByTestId(DemosTestIds.DemoTokensError);

    public ILocator TokensHints => page.GetByTestId(DemosTestIds.DemoTokensHints);

    public ILocator YouTubePanel => page.GetByTestId(DemosTestIds.YoutubePanel);

    public ILocator YouTubeOff => page.GetByTestId(DemosTestIds.YoutubeOff);

    public ILocator YouTubeConnect => page.GetByTestId(DemosTestIds.YoutubeConnect);

    public ILocator YouTubeChannel => page.GetByTestId(DemosTestIds.YoutubeChannel);

    public ILocator YouTubeVisibility => page.GetByTestId(DemosTestIds.YoutubeVisibility);

    public ILocator YouTubeCheck => page.GetByTestId(DemosTestIds.YoutubeCheck);

    public ILocator YouTubeRefusal => page.GetByTestId(DemosTestIds.YoutubeRefusal);

    public ILocator YouTubeConnected => page.GetByTestId(DemosTestIds.YoutubeConnected);

    public ILocator YouTubeDisconnect => page.GetByTestId(DemosTestIds.YoutubeDisconnect);

    public ILocator InstanceClientId => page.GetByTestId(DemosTestIds.InstanceYoutubeClientId);

    public ILocator InstanceClientSecret => page.GetByTestId(DemosTestIds.InstanceYoutubeClientSecret);

    public ILocator InstanceFromEnv => page.GetByTestId(DemosTestIds.InstanceYoutubeFromEnv);

    public async Task GotoAsync(string projectKey)
    {
        await page.GotoAsync(Links.ProjectSettings(projectKey, "demos"));
        await Panel.WaitForAsync();
    }

    public Task UploadLogoAsync(string fileName, string mimeType, byte[] bytes) =>
        LogoFile.SetInputFilesAsync(new FilePayload { Name = fileName, MimeType = mimeType, Buffer = bytes });

    public async Task SaveTokensAsync(string tokens)
    {
        await Tokens.FillAsync(tokens);
        await TokensSave.ClickAsync();
    }

    public Task ConnectAsync() => YouTubeConnect.ClickAsync();

    public Task CheckAsync() => YouTubeCheck.ClickAsync();
}
