using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Read;

// The Loki names come from the settings' field names ('.' to '_'), never from the defaults.
[TestFixture(TestName = "Loki field mapping")]
public sealed class LokiFieldMappingTests
{
    [Test]
    public async Task Load_WhenTheSessionFieldIsRenamed_UsesTheRenamedLabelInTheLogQl()
    {
        var options = ReaderStores.Options with { Fields = InspectionFieldNames.Defaults with { Session = "agent.run-id" } };
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(), "{\"traces\":[]}", StoreJson.EmptyLoki);

        await ReaderStores.Reader(stores, options).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        var logQl = Uri.UnescapeDataString(stores.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains("/loki/", StringComparison.Ordinal)).RequestUri!.Query);
        logQl.ShouldContain("agent_run_id=\"s1\"");
        logQl.ShouldNotContain("session_id=");
    }

    [Test]
    public async Task Load_WhenLokiReturnsMappedNames_GivesThemBackDottedAndKeepsOtherUnderscoredKeys()
    {
        var loki = "{\"data\":{\"result\":[{\"stream\":{\"service_name\":\"claude-code\",\"shiplean_project\":\"SHIP\",\"shiplean_item\":\"SHIP-1\",\"session_id\":\"s1\",\"tool_name\":\"Edit\"},\"values\":[[\"" + StoreJson.StartNanos + "\",\"x\"]]}]}}";
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(), "{\"traces\":[]}", loki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldNotBeNull();
        var attributes = run.Logs.Single().Attributes;
        attributes["session.id"].ShouldBe("s1");
        attributes["shiplean.item"].ShouldBe("SHIP-1");
        attributes["tool_name"].ShouldBe("Edit");
        run.Logs.Single().SessionId.ShouldBe("s1");
    }
}
