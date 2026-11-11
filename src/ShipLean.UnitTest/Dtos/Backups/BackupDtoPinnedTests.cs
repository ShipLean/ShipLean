using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.UnitTest.Persisted;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Backups;

// Every example of the five backup-*.json files survives a round trip through its DTO with the API's options.
[TestFixture]
public class BackupDtoPinnedTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static IEnumerable<TestCaseData> Examples()
    {
        foreach (var (file, type) in new[]
        {
            ("backup-settings.json", typeof(BackupSettingsDto)),
            ("backup-save.json", typeof(BackupSaveDto)),
            ("backup-run.json", typeof(BackupRunDto)),
            ("backup-cron-preview.json", typeof(CronPreviewDto)),
            ("backup-test-result.json", typeof(BackupTestResultDto)),
        })
        {
            foreach (var example in PinnedFixture.Read(file).AsObject())
            {
                yield return new TestCaseData(file, example.Key, type).SetArgDisplayNames(file, example.Key);
            }
        }
    }

    [TestCaseSource(nameof(Examples))]
    public void Example_WhenRoundTripped_ShouldMatchThePinnedFile(string file, string example, Type type)
    {
        var document = PinnedFixture.Read(file)[example]!;

        var dto = document.Deserialize(type, Options);

        dto.ShouldNotBeNull();
        var written = JsonSerializer.SerializeToNode(dto, type, Options);
        JsonNode.DeepEquals(document, written).ShouldBeTrue(written?.ToJsonString());
    }

    [Test]
    public void Files_WhenListed_ShouldEachHaveARoundTrip()
    {
        PinnedFixture.Files("backup-*.json").ShouldBe(
            ["backup-cron-preview.json", "backup-run.json", "backup-save.json", "backup-settings.json", "backup-test-result.json"]);
    }

    [Test]
    public void Settings_WhenConfiguredRead_ShouldCarryTypedValues()
    {
        var dto = PinnedFixture.Read("backup-settings.json")["configured"].Deserialize<BackupSettingsDto>(Options)!;

        dto.Health.ShouldBe(BackupHealth.Ok);
        dto.Destination!.Provider.ShouldBe(BackupProvider.S3);
        dto.AttachmentStore.Kind.ShouldBe(BackupAttachmentStoreKind.LocalDisk);
        dto.LastSuccessAt.ShouldBe(new DateTimeOffset(2026, 10, 24, 2, 0, 47, TimeSpan.Zero));
        dto.Runs.Select(r => r.State).ShouldBe([BackupRunState.Succeeded, BackupRunState.Failed, BackupRunState.Failed]);
        dto.Regions.ShouldBe(BackupRegions.All);
    }

    [Test]
    public void Settings_WhenNotConfiguredRead_ShouldHaveNoDestination()
    {
        var dto = PinnedFixture.Read("backup-settings.json")["notConfigured"].Deserialize<BackupSettingsDto>(Options)!;

        dto.Health.ShouldBe(BackupHealth.NotConfigured);
        dto.Destination.ShouldBeNull();
        dto.PassphraseSetAt.ShouldBeNull();
        dto.Regions.ShouldBe(BackupRegions.All);
    }

    [Test]
    public void TestResult_WhenFailedRead_ShouldCarryTheStep()
    {
        var dto = PinnedFixture.Read("backup-test-result.json")["failed"].Deserialize<BackupTestResultDto>(Options)!;

        dto.Step.ShouldBe(BackupTestStep.Write);
    }

    [TestCase("\"2026-10-24T14:00:00+00:00\"")]
    [TestCase("\"2026-10-24T14:00:00\"")]
    [TestCase("\"2026-10-24T16:00:00+02:00\"")]
    public void Timestamp_WhenNotUtcZ_ShouldBeRejected(string json)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CronRunDto>($$"""{"at":{{json}},"label":"x"}""", Options));
    }

    [Test]
    public void Timestamp_WhenWritten_ShouldBeUtcZToTheSecond()
    {
        var run = new CronRunDto(new DateTimeOffset(2026, 10, 24, 16, 0, 0, 123, TimeSpan.FromHours(2)), "x");

        JsonSerializer.SerializeToNode(run, Options)!["at"]!.GetValue<string>().ShouldBe("2026-10-24T14:00:00Z");
    }

    private const string Secret = "example-secret-not-a-real-key-7Hq2";
    private const string Passphrase = "correct horse battery staple";

    private static S3DestinationSaveDto S3(string? secretKey) => new("us-east-1", null, "shiplean-backups", "shiplean/", false, "SHIPLEANEXAMPLEKEY01", secretKey);

    [Test]
    public void S3DestinationSaveDto_WhenPrinted_ShouldHideTheSecretKey()
    {
        var text = S3(Secret).ToString();

        text.ShouldNotContain(Secret);
        text.ShouldContain("SecretKey = ***");
        text.ShouldContain("shiplean-backups");
    }

    [Test]
    public void BackupDestinationSaveDto_WhenPrinted_ShouldHideTheSecretKey()
    {
        var text = new BackupDestinationSaveDto(BackupProvider.S3, S3(Secret)).ToString();

        text.ShouldNotContain(Secret);
        text.ShouldContain("SecretKey = ***");
    }

    [Test]
    public void BackupSaveDto_WhenPrinted_ShouldHideThePassphraseAndSecretKey()
    {
        var text = new BackupSaveDto(true, new BackupDestinationSaveDto(BackupProvider.S3, S3(Secret)), "0 2 * * *", 14, true, Passphrase).ToString();

        text.ShouldNotContain(Passphrase);
        text.ShouldNotContain(Secret);
        text.ShouldContain("Passphrase = ***");
        text.ShouldContain("0 2 * * *");
    }
}
