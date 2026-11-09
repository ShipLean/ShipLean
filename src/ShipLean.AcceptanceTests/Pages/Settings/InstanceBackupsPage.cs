using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The Backups tab of Instance settings.
public sealed class InstanceBackupsPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(BackupsTestIds.BackupsPage);

    public ILocator Health => page.GetByTestId(BackupsTestIds.BackupsHealth);

    public ILocator EndpointMode => page.GetByTestId(BackupsTestIds.BackupsEndpointMode);

    public ILocator EndpointUrl => page.GetByTestId(BackupsTestIds.BackupsEndpointUrl);

    // In custom-endpoint mode this is the signing-region text input.
    public ILocator Region => page.GetByTestId(BackupsTestIds.BackupsRegionSelect);

    public ILocator Bucket => page.GetByTestId(BackupsTestIds.BackupsBucket);

    public ILocator AccessKeyId => page.GetByTestId(BackupsTestIds.BackupsAccessKeyId);

    public ILocator SecretKey => page.GetByTestId(BackupsTestIds.BackupsSecretKey);

    public ILocator SecretKeyReplace => page.GetByTestId(BackupsTestIds.BackupsSecretKeyReplace);

    public ILocator SecretKeyLastFour => page.GetByTestId(BackupsTestIds.BackupsSecretKeyLastFour);

    public ILocator Passphrase => page.GetByTestId(BackupsTestIds.BackupsPassphrase);

    public ILocator PassphraseConfirm => page.GetByTestId(BackupsTestIds.BackupsPassphraseConfirm);

    public ILocator CronInput => page.GetByTestId(BackupsTestIds.BackupsCronInput);

    public ILocator CronError => page.GetByTestId(BackupsTestIds.BackupsCronError);

    public ILocator CronNextRuns => page.GetByTestId(BackupsTestIds.BackupsCronNextRun);

    public ILocator SaveButton => page.GetByTestId(BackupsTestIds.BackupsSaveButton);

    public ILocator TestButton => page.GetByTestId(BackupsTestIds.BackupsTestButton);

    public ILocator TestResult => page.GetByTestId(BackupsTestIds.BackupsTestResult);

    public ILocator RunNowButton => page.GetByTestId(BackupsTestIds.BackupsRunNow);

    public ILocator RunRows => page.GetByTestId(BackupsTestIds.BackupsRunRow);

    public ILocator Preset(string label) =>
        page.GetByTestId(BackupsTestIds.BackupsCronPreset).Filter(new() { HasTextString = label });

    public async Task OpenAsync()
    {
        await page.GotoAsync(Links.InstanceSettings("backups"));
        await Panel.WaitForAsync();
    }

    // Types a custom-endpoint destination, a secret and a passphrase into the form without saving. A secret already
    // saved by an earlier test of the class is replaced; a passphrase typed again starts the next generation.
    public async Task FillDestinationAsync(string endpoint, string bucket, string accessKey, string secretKey, string passphrase)
    {
        await EndpointMode.SelectOptionAsync("custom");
        await EndpointUrl.FillAsync(endpoint);
        await Region.FillAsync("us-east-1");
        await Bucket.FillAsync(bucket);
        await AccessKeyId.FillAsync(accessKey);
        if (await SecretKeyReplace.IsVisibleAsync())
        {
            await SecretKeyReplace.ClickAsync();
        }

        await SecretKey.FillAsync(secretKey);
        await Passphrase.FillAsync(passphrase);
        await PassphraseConfirm.FillAsync(passphrase);
    }

    // Saves and waits for the response, so a following reload does not abort the write.
    public async Task SaveAsync() =>
        await page.RunAndWaitForApiAsync("/api/instance/backups", "PUT", () => SaveButton.ClickAsync());
}
