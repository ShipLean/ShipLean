using Microsoft.Extensions.Configuration;
using ShipLean.Application.Features.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// Bad SHIPLEAN_INSPECTION_* values stop startup (InspectionServices resolves the options from a hosted service) naming the variable.
[TestFixture]
public class InspectionOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    [Test]
    public void From_WhenNothingSet_ShouldBeOff()
    {
        InspectionOptionsReader.From(Config()).Enabled.ShouldBeFalse();
    }

    [Test]
    public void From_WhenSwitchUnknown_ShouldNameTheVariable()
    {
        Should.Throw<InvalidOperationException>(() => InspectionOptionsReader.From(Config(("SHIPLEAN_INSPECTION", "maybe")))).Message.ShouldContain("SHIPLEAN_INSPECTION");
    }

    [Test]
    public void From_WhenOnWithoutStore_ShouldNameTheStoreVariable()
    {
        Should.Throw<InvalidOperationException>(() => InspectionOptionsReader.From(Config(("SHIPLEAN_INSPECTION", "on")))).Message.ShouldContain("SHIPLEAN_INSPECTION_STORE");
    }

    [Test]
    public void From_WhenTableNameIsNotAnIdentifier_ShouldNameTheVariable()
    {
        var error = Should.Throw<InvalidOperationException>(() => InspectionOptionsReader.From(Config(("SHIPLEAN_INSPECTION_CLICKHOUSE_TRACES_TABLE", "otel;drop"))));

        error.Message.ShouldContain("SHIPLEAN_INSPECTION_CLICKHOUSE_TRACES_TABLE");
    }

    [Test]
    public void From_WhenClickHouseWithConnection_ShouldBeOn()
    {
        var options = InspectionOptionsReader.From(Config(
            ("SHIPLEAN_INSPECTION", "on"), ("SHIPLEAN_INSPECTION_STORE", "clickhouse"), ("SHIPLEAN_INSPECTION_CLICKHOUSE_CONNECTION", "Host=x")));

        options.Enabled.ShouldBeTrue();
        options.Store.ShouldBe("clickhouse");
    }
}
