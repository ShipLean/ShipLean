using System.Text.Json;
using ShipLean.Application.Features.Mcp.Sprint;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Mcp;

// SprintFill.Cut against the cases the client's pull line reads too (pull-cut-cases.json), so the two rules can't drift.
[TestFixture]
public class SprintFillTests
{
    private static readonly JsonElement Document = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "backlog", "pull-cut-cases.json"))).RootElement;

    public static IEnumerable<string> CaseNames => Document.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("name").GetString()!);

    [TestCaseSource(nameof(CaseNames))]
    public void Cut_WhenCaseRun_ShouldMatchTheSharedCases(string name)
    {
        var testCase = Document.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);
        var expect = testCase.GetProperty("expect");

        var result = SprintFill.Cut(Stack(testCase), testCase.GetProperty("room").GetDecimal(), testCase.GetProperty("alreadyIn").GetDecimal());

        result.Selected.Count.ShouldBe(expect.GetProperty("selected").ValueKind == JsonValueKind.String ? SprintFill.MaxItems : expect.GetProperty("selected").GetInt32());
        result.SprintPoints.ShouldBe(expect.GetProperty("sprintPoints").GetDecimal());
        result.OverBy.ShouldBe(expect.GetProperty("overBy").ValueKind == JsonValueKind.Null ? null : expect.GetProperty("overBy").GetDecimal());
        result.Capped.ShouldBe(expect.GetProperty("capped").GetBoolean());
    }

    private static List<StackRowDto> Stack(JsonElement testCase)
    {
        var points = testCase.TryGetProperty("stack", out var stack)
            ? stack.EnumerateArray().Select(p => p.ValueKind == JsonValueKind.Null ? (decimal?)null : p.GetDecimal()).ToList()
            : Enumerable.Repeat((decimal?)testCase.GetProperty("pointsEach").GetDecimal(), testCase.GetProperty("rows").GetInt32()).ToList();
        return [.. points.Select((p, index) => new StackRowDto(
            Guid.NewGuid(), $"SHIP-{index + 1}", $"Row {index + 1}", new TypeRef(WorkItemTypeId.Story, "User Story"),
            new StateRef(Guid.NewGuid(), "New", StateCategory.Proposed), p, null, null, null, false, DateTimeOffset.UnixEpoch, [], 1))];
    }
}
