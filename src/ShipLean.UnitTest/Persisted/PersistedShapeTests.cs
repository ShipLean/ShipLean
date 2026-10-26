using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.UnitTest.Persisted;

[TestFixture]
public class PersistedShapeTests
{
    private static IEnumerable<string> CriteriaFiles() => PinnedFixture.Files("criteria.*.json");

    private static IEnumerable<string> BoardFiles() => PinnedFixture.Files("board-settings.*.json");

    [TestCaseSource(nameof(CriteriaFiles))]
    public void CriteriaJson_WhenRoundTripped_ShouldMatchTheFixture(string file)
    {
        PinnedFixture.SurvivesRoundTrip<CriteriaJson>(PinnedFixture.Read(file)).ShouldBeTrue();
    }

    [TestCaseSource(nameof(BoardFiles))]
    public void BoardSettingsJson_WhenRoundTripped_ShouldMatchTheFixture(string file)
    {
        PinnedFixture.SurvivesRoundTrip<BoardSettingsJson>(PinnedFixture.Read(file)).ShouldBeTrue();
    }

    [Test]
    public void FieldChange_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var revisions = PinnedFixture.Read("field-change.json")["revisions"]!.AsArray();
        revisions.Count.ShouldBeGreaterThan(0);
        foreach (var revision in revisions)
        {
            PinnedFixture.SurvivesRoundTrip<List<FieldChange>>(revision!["changes"]!).ShouldBeTrue();
        }
    }

    [Test]
    public void ImportJobProgressJson_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var progress = PinnedFixture.Read("import-job-state.json")["progress"]!.AsObject();
        progress.Count.ShouldBe(3);
        foreach (var (_, example) in progress)
        {
            PinnedFixture.SurvivesRoundTrip<ImportJobProgressJson>(example!["document"]!).ShouldBeTrue();
        }
    }

    [Test]
    public void ImportJobState_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var file = PinnedFixture.Read("import-job-state.json");
        JsonNode.DeepEquals(file["importJobState"], PinnedFixture.RoundTrip<List<ImportJobState>>(file["importJobState"]!)).ShouldBeTrue();
        JsonNode.DeepEquals(file["importStep"], PinnedFixture.RoundTrip<List<ImportStep>>(file["importStep"]!)).ShouldBeTrue();
    }

    [Test]
    public void Refs_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var file = PinnedFixture.Read("refs.json");
        PinnedFixture.SurvivesRoundTrip<List<UserRef>>(file["userRefs"]!).ShouldBeTrue();
        PinnedFixture.SurvivesRoundTrip<List<SprintRef>>(file["sprintRefs"]!).ShouldBeTrue();
        PinnedFixture.SurvivesRoundTrip<List<TypeRef>>(file["typeRefs"]!).ShouldBeTrue();
        foreach (var (type, states) in file["stateRefs"]!.AsObject())
        {
            PinnedFixture.SurvivesRoundTrip<List<StateRef>>(states!).ShouldBeTrue();
            type.ShouldNotBeNullOrEmpty();
        }
    }

    [Test]
    public void FieldIds_ShouldMatchTheVocabularyFixture()
    {
        var fixture = PinnedFixture.Read("field-ids.json");
        var written = JsonSerializer.SerializeToNode(FieldIds.All, PersistedJson.Options)!;

        JsonNode.DeepEquals(fixture, written).ShouldBeTrue(written.ToJsonString());
        PinnedFixture.SurvivesRoundTrip<List<FieldDefinition>>(fixture).ShouldBeTrue();
        FieldIds.All.Select(f => f.Id).ShouldBe(fixture.AsArray().Select(f => (string)f!["id"]!));
        FieldIds.Priority.ShouldBe("priority");
        FieldIds.AcceptanceCriteria.ShouldBe("acceptanceCriteria");
    }

    [Test]
    public void Enums_WhenUnknownValue_ShouldFailToDeserialise()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<StateCategory>("\"nonsense\"", PersistedJson.Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<StateCategory>("1", PersistedJson.Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CriteriaOp>("\">=\"", PersistedJson.Options));
        foreach (var text in new[] { "\"DoneWithErrors\"", "\"donewitherrors\"", "\"queued, running\"" })
        {
            Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ImportJobState>(text, PersistedJson.Options));
        }
        JsonSerializer.Deserialize<ImportJobState>("\"doneWithErrors\"", PersistedJson.Options).ShouldBe(ImportJobState.DoneWithErrors);
    }

    [Test]
    public void PersistedJson_WhenUnknownProperty_ShouldFail()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<UserRef>(UserJson("name", "\"extra\": 1"), PersistedJson.Options));
    }

    [Test]
    public void PersistedJson_WhenWrongCaseProperty_ShouldFail()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<UserRef>(UserJson("Name", ""), PersistedJson.Options));
    }

    [Test]
    public void FieldChange_WhenBuiltFromJsonNull_ShouldSerialiseNull()
    {
        var written = JsonSerializer.SerializeToNode(new FieldChange("points", JsonNull.Value, JsonNull.Value, null, null), PersistedJson.Options)!;

        written.AsObject().ContainsKey("to").ShouldBeTrue();
        written["from"].ShouldBeNull();
        written["to"].ShouldBeNull();
    }

    private static string UserJson(string nameKey, string extra) =>
        "{\"id\": \"00000001-0000-4000-8000-000000000002\", \"" + nameKey + "\": \"A\", \"initials\": \"AB\", \"avatarColour\": \"#000000\", \"photoUrl\": null" + (extra == "" ? "" : ", " + extra) + "}";
}
