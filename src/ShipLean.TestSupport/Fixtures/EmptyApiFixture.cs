using ShipLean.TestSupport.Database;

namespace ShipLean.TestSupport.Fixtures;

// An ApiFixture whose database has the schema and migrations but no seed rows.
public abstract class EmptyApiFixture : ApiFixture
{
    protected override Task<ClonedDatabase> CloneDatabaseAsync() => ShipLeanDatabase.CloneEmptyAsync();
}
