using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// What an upload reports about itself: its kind, its search text and who attached it, through the real app.
[TestFixture]
public class AttachmentKindApiTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    [Test]
    public async Task Upload_WhenMarkdown_ShouldReturnKindMarkdownAndStoreSearchText()
    {
        const string text = "# Vault notes\n\nTokens expire after an hour.\n";

        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "vault-notes.md", "text/markdown", Encoding.UTF8.GetBytes(text));

        added.Kind.ShouldBe(AttachmentKind.Markdown);
        added.AgentName.ShouldBeNull();
        added.Via.ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<string?>("SELECT search_text FROM attachment WHERE id = @id", new { id = added.Id })).ShouldBe(text);
    }

    [Test]
    public async Task Upload_WhenMarkdownOverOneMegabyte_ShouldStoreNoSearchText()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "big.md", "text/markdown", Encoding.UTF8.GetBytes(new string('a', 1024 * 1024 + 1)));

        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<string?>("SELECT search_text FROM attachment WHERE id = @id", new { id = added.Id })).ShouldBeNull();
    }

    [Test]
    public async Task Upload_WhenNotMarkdown_ShouldStoreNoSearchText()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "notes.txt", "text/plain", "plain text"u8.ToArray());

        added.Kind.ShouldBe(AttachmentKind.File);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<string?>("SELECT search_text FROM attachment WHERE id = @id", new { id = added.Id })).ShouldBeNull();
    }

    [TestCase("shot.png", "image/png", AttachmentKind.Image)]
    [TestCase("logo.SVG", "application/octet-stream", AttachmentKind.Image)]
    [TestCase("photo", "image/webp", AttachmentKind.Image)]
    [TestCase("README.MD", "application/octet-stream", AttachmentKind.Markdown)]
    [TestCase("notes", "text/markdown; charset=utf-8", AttachmentKind.Markdown)]
    [TestCase("data.csv", "text/csv", AttachmentKind.File)]
    public async Task Upload_WhenNameAndTypeGiven_ShouldReturnItsKind(string name, string contentType, AttachmentKind kind)
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, name, contentType, [1, 2, 3]);

        added.Kind.ShouldBe(kind);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Single(a => a.Id == added.Id).Kind.ShouldBe(kind);
    }

    [Test]
    public async Task Upload_WhenAgentSource_ShouldReturnAgentNameAndVia()
    {
        using var scope = Host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAttachmentStore>();

        var added = await store.AddAsync(
            SeedIds.WorkItems.Ship90, "agent.md", "text/markdown", new MemoryStream("# By an agent"u8.ToArray()),
            new AttachmentSource("Claude Code (claude.ai)", "attach_file"));

        added.AgentName.ShouldBe("Claude Code (claude.ai)");
        added.Via.ShouldBe("attach_file");
        var listed = (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Single(a => a.Id == added.Id);
        listed.AgentName.ShouldBe("Claude Code (claude.ai)");
        listed.Via.ShouldBe("attach_file");
    }
}
