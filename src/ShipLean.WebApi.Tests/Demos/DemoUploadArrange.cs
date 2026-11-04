using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Demos;

// A BAU item named by a sprint's demo_work_item_id and a live demo token for it, the way K6's decision leaves them, for the upload's tests.
internal static class DemoUploadArrange
{
    public const string AgentStamp = "Test Agent (unverified)";

    public sealed record Arranged(Guid SprintId, Guid ItemId, string Token, Guid UserId)
    {
        public string Path => $"api/uploads/demo?token={Uri.EscapeDataString(Token)}";
    }

    public static async Task<Arranged> NewAsync(NpgsqlConnection database, IServiceProvider services, Guid userId, Guid? authorizationId = null)
    {
        var itemId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").WithTitle("Sprint 23 demo").InsertAsync(database);
        var sprintId = await DemoRunArrange.NewSprintAsync(database, SeedIds.Projects.Ship, $"Sprint {Guid.NewGuid():N}");
        await database.ExecuteAsync("UPDATE sprint SET demo_work_item_id = @itemId WHERE id = @sprintId", new { itemId, sprintId });
        await using var scope = services.CreateAsyncScope();
        var issued = await scope.ServiceProvider.GetRequiredService<ITransferTokens>().IssueAsync(
            new NewTransfer(TransferKind.Demo, itemId, null, "sprint-demo.mp4", "video/mp4", userId, authorizationId, AgentStamp, TimeSpan.FromHours(24)));
        return new Arranged(sprintId, itemId, issued.Token, userId);
    }

    // The real fixture MP4 (an `ftyp` box at offset 4).
    public static byte[] Mp4() => File.ReadAllBytes(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4"));

    // An MP4 header followed by zeros, to make a video of a size.
    public static byte[] Mp4OfSize(int length)
    {
        var bytes = new byte[length];
        Mp4()[..12].CopyTo(bytes, 0);
        return bytes;
    }

    public static MultipartFormDataContent Form(string chapters, string script, byte[] video, params string[] order)
    {
        var parts = new Dictionary<string, (byte[] Bytes, string File)>
        {
            ["chapters"] = (System.Text.Encoding.UTF8.GetBytes(chapters), "chapters.json"),
            ["script"] = (System.Text.Encoding.UTF8.GetBytes(script), "script.md"),
            ["video"] = (video, "demo.mp4"),
        };
        var form = new MultipartFormDataContent();
        foreach (var name in order.Length == 0 ? ["chapters", "script", "video"] : order)
        {
            form.Add(new ByteArrayContent(parts[name].Bytes), name, parts[name].File);
        }

        return form;
    }
}
