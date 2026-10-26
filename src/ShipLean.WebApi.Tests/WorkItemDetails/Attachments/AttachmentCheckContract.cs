using System.Net.Http.Json;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// What the check endpoint must answer once the upload rules are real. Red until A1 (the stub answers "add" for every file).
[TestFixture]
public class AttachmentCheckContract : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private async Task<AttachmentCheckOutcomeDto> CheckOneAsync(string name, long sizeBytes)
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/check", new AttachmentCheckRequest([new(name, sizeBytes, "application/octet-stream")]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttachmentCheckDto>())!.Files.Single();
    }

    [Test]
    public async Task Check_WhenNewName_ShouldBeAdd()
    {
        (await CheckOneAsync("brand-new-file.bin", 10)).Outcome.ShouldBe(AttachmentOutcome.Add);
    }

    [Test]
    public async Task Check_WhenNameIsAlreadyAttached_ShouldBeReplace()
    {
        await AttachmentUploads.UploadAsync(Client, ItemUrl, "existing.bin", "application/octet-stream", [1]);

        (await CheckOneAsync("existing.bin", 10)).Outcome.ShouldBe(AttachmentOutcome.Replace);
    }

    [Test]
    public async Task Check_WhenOverTheFileLimit_ShouldBeTooLargeWithAMessage()
    {
        var outcome = await CheckOneAsync("huge.bin", 26L * 1024 * 1024);

        outcome.Outcome.ShouldBe(AttachmentOutcome.TooLarge);
        outcome.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Check_WhenTheFilesTogetherPassTheItemLimit_ShouldBeOverTotal()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/check", new AttachmentCheckRequest(
            [.. Enumerable.Range(0, 11).Select(i => new AttachmentCheckFile($"part-{i}.bin", 24L * 1024 * 1024, "application/octet-stream"))]));
        response.EnsureSuccessStatusCode();

        var files = (await response.Content.ReadFromJsonAsync<AttachmentCheckDto>())!.Files;

        files.ShouldContain(f => f.Outcome == AttachmentOutcome.OverTotal);
    }
}
