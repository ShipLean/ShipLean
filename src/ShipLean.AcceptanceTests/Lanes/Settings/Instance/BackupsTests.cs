using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.Application.Storage;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Storage;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Instance;

// An instance admin sets up scheduled backups on the Backups tab against a real S3-compatible store (RustFS), the real
// pg_dump and the real worker. The acceptance host and the store run on the same machine, so one URL serves the form
// and the host. Tests do not depend on each other: each types what it needs, whatever an earlier test saved.
[TestFixture]
public class BackupsTests : SeededAcceptanceTest
{
    private const string Passphrase = "larkspur passphrase 17";
    private const int RunTimeoutMs = 120_000;

    private S3BlobConnection _bucket = null!;

    private string AccessKey => _bucket.AccessKey!;

    private string SecretKey => _bucket.SecretKey!;

    private string Endpoint => _bucket.ServiceUrl!.ToString().TrimEnd('/');

    [OneTimeSetUp]
    public async Task CreateBucketAsync() => _bucket = await S3Server.NewBucketAsync();

    [Test]
    public async Task Configure_WhenAdminSaves_ShouldShowNextRuns()
    {
        var backups = new InstanceBackupsPage(Page);
        await backups.OpenAsync();

        await backups.FillDestinationAsync(Endpoint, _bucket.Bucket, AccessKey, SecretKey, Passphrase);
        await backups.Preset("Sundays 02:00").ClickAsync();
        await Expect(backups.CronNextRuns.First).ToBeVisibleAsync();
        await backups.EnabledToggle.CheckAsync();
        await Expect(backups.SaveButton).ToBeEnabledAsync();
        await backups.TestButton.ClickAsync();
        await Expect(backups.TestResult).ToHaveAttributeAsync("role", "status");
        await backups.SaveAsync();
        await Page.ReloadAsync();

        await Expect(backups.Bucket).ToHaveValueAsync(_bucket.Bucket);
        await Expect(backups.EndpointUrl).ToHaveValueAsync(Endpoint);
        await Expect(backups.CronInput).ToHaveValueAsync("0 2 * * 0");
        await Expect(backups.EnabledToggle).ToBeCheckedAsync();
        await Expect(backups.CronDescription).ToContainTextAsync("Sunday");
        await Expect(backups.SecretKeyLastFour).ToHaveTextAsync($"•••• {SecretKey[^4..]}");
        await Expect(backups.CronNextRuns.First).ToBeVisibleAsync();
        foreach (var label in await backups.CronNextRuns.AllTextContentsAsync())
        {
            label.ShouldMatch(@"^\d{4}-\d{2}-\d{2} 02:00 UTC$");
        }

        await Expect(backups.Panel).Not.ToContainTextAsync(SecretKey);
        await Expect(backups.Panel).Not.ToContainTextAsync(Passphrase);
        var read = await Page.Context.APIRequest.GetAsync("/api/instance/backups");
        var body = await read.TextAsync();
        body.ShouldNotContain(SecretKey);
        body.ShouldNotContain(Passphrase);
    }

    [Test]
    public async Task RunNow_WhenAdminClicks_ShouldShowSucceededRun()
    {
        var backups = new InstanceBackupsPage(Page);
        await backups.OpenAsync();
        await backups.FillDestinationAsync(Endpoint, _bucket.Bucket, AccessKey, SecretKey, Passphrase);
        await backups.Preset("Daily 02:00").ClickAsync();
        await Expect(backups.SaveButton).ToBeEnabledAsync();
        await backups.SaveAsync();
        await Expect(backups.RunNowButton).ToBeEnabledAsync();
        var before = await backups.RunRows.CountAsync();

        await backups.RunNowButton.ClickAsync();

        await Expect(backups.RunRows).ToHaveCountAsync(before + 1, new() { Timeout = RunTimeoutMs });
        await Expect(backups.RunRows.First).ToHaveAttributeAsync("data-state", "succeeded", new() { Timeout = RunTimeoutMs });
        await Expect(backups.RunRows.First).ToContainTextAsync("Manual");
        await Expect(backups.Health).ToHaveAttributeAsync("data-health", "ok", new() { Timeout = RunTimeoutMs });

        using var client = S3Server.CreateClient(_bucket);
        var listed = await client.ListObjectsV2Async(new() { BucketName = _bucket.Bucket, Prefix = "shiplean/database/" });
        listed.S3Objects.Count.ShouldBe(1);
    }

    [Test]
    public async Task Cron_WhenInvalid_ShouldShowErrorAndDisableSave()
    {
        var backups = new InstanceBackupsPage(Page);
        await backups.OpenAsync();
        await backups.FillDestinationAsync(Endpoint, _bucket.Bucket, AccessKey, SecretKey, Passphrase);
        await backups.Preset("Hourly").ClickAsync();
        await Expect(backups.SaveButton).ToBeEnabledAsync();

        await backups.CronInput.FillAsync("61 * * * *");

        await Expect(backups.CronError).ToBeVisibleAsync();
        await Expect(backups.SaveButton).ToBeDisabledAsync();

        await backups.Preset("Hourly").ClickAsync();

        await Expect(backups.SaveButton).ToBeEnabledAsync();
        await Expect(backups.CronError).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Save_WhenBucketNameInvalid_ShouldShowServerFieldError()
    {
        var backups = new InstanceBackupsPage(Page);
        await backups.OpenAsync();
        await backups.FillDestinationAsync(Endpoint, "Bad_Bucket", AccessKey, SecretKey, Passphrase);
        await backups.Preset("Hourly").ClickAsync();
        await Expect(backups.SaveButton).ToBeEnabledAsync();

        var response = await backups.SaveAsync();

        response.Status.ShouldBe(400);
        // The client only checks that a bucket is given, so this message is the server's, shown next to the bucket input.
        await Expect(backups.Bucket).ToHaveAccessibleDescriptionAsync(new Regex("lowercase letters"));
        await Expect(Page.GetByTestId(BackupsTestIds.BackupsFieldError)).ToHaveCountAsync(1);
        await Expect(Page.GetByTestId(BackupsTestIds.BackupsFormError)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task TestConnection_WhenBucketMissing_ShouldShowFailingStep()
    {
        var backups = new InstanceBackupsPage(Page);
        await backups.OpenAsync();
        var missing = $"missing-{Guid.NewGuid():N}"[..30];
        await backups.FillDestinationAsync(Endpoint, _bucket.Bucket, AccessKey, SecretKey, Passphrase);

        await backups.TestButton.ClickAsync();

        await Expect(backups.TestResult).ToHaveAttributeAsync("role", "status");

        await backups.Bucket.FillAsync(missing);
        await backups.TestButton.ClickAsync();

        await Expect(backups.TestResult).ToContainTextAsync("Write failed");
    }

    // Mei is a project member, not an instance admin.
    [Test]
    public async Task Tab_WhenNotAdmin_ShouldNotShow()
    {
        await SignOutAsync();
        await SignInAsync(TestUser.Mei.Email!, TestUser.Mei.Password!);
        var backups = new InstanceBackupsPage(Page);
        await Page.GotoAsync(Links.InstanceSettings("backups"));

        await Expect(new InstanceGeneralPage(Page).Denied).ToBeVisibleAsync();
        await Expect(backups.Panel).ToHaveCountAsync(0);
        await Expect(backups.RunNowButton).ToHaveCountAsync(0);
        var read = await Page.Context.APIRequest.GetAsync("/api/instance/backups");
        read.Status.ShouldBe(403);
    }
}
