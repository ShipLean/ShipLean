using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.TaskChanges;

// The wire is camelCase and nothing is omitted, so each fixture must survive a read into its DTO and a write back unchanged.
[TestFixture]
public class TaskChangesDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Fixture(string file, string name)
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "taskChanges", file);
        return JsonNode.Parse(File.ReadAllText(path))![name]!;
    }

    [TestCase("section.json", "section", typeof(TaskCommitsSectionDto))]
    [TestCase("changes-combined.json", "changesCombined", typeof(TaskChangesDto))]
    [TestCase("changes-per-commit.json", "changesPerCommit", typeof(TaskChangesDto))]
    [TestCase("changes-noaccess.json", "changesNoAccess", typeof(TaskChangesDto))]
    [TestCase("comment.json", "comment", typeof(CodeCommentDto))]
    [TestCase("review-outcome.json", "approved", typeof(ReviewOutcomeDto))]
    [TestCase("review-outcome.json", "requested", typeof(ReviewOutcomeDto))]
    public void TaskChangesDtos_WhenSerialized_ShouldMatchFixtures(string file, string name, Type dto)
    {
        var fixture = Fixture(file, name);

        var value = fixture.Deserialize(dto, Options);

        JsonNode.DeepEquals(fixture, JsonSerializer.SerializeToNode(value, dto, Options)).ShouldBeTrue();
    }

    [Test]
    public void TaskChangesFixtures_WhenTwoRepositoriesAreShown_ShouldKeepEachPartInItsRepository()
    {
        var value = Fixture("changes-per-commit.json", "changesPerCommit").Deserialize<TaskChangesDto>(Options)!;

        value.Mode.ShouldBe("perCommit");
        value.Parts.Select(part => part.Repo).ShouldBe(["acme/storefront", "acme/storefront", "acme/payments"]);
        value.Parts.ShouldAllBe(part => part.Sha == part.HeadSha && part.BaseSha == null);
    }

    [Test]
    public void TaskChangesFixtures_WhenAFileIsHashed_ShouldCarryTheSha1OfItsPatch()
    {
        var value = Fixture("changes-combined.json", "changesCombined").Deserialize<TaskChangesDto>(Options)!;

        foreach (var file in value.Parts.SelectMany(part => part.Files))
        {
            file.PatchId.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(file.Patch))));
        }
    }
}
