using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests.Harness;

// Base class for a scenario that starts from the seed. The class gets its own cloned copy of the seed template and
// its own app instance; tests inside run in [Order]. Put a lane's class under Lanes/<Area>/ in namespace
// ShipLean.AcceptanceTests.Lanes.<Area>.
public abstract class SeededAcceptanceTest : AcceptanceFixture
{
    protected override Task<ClonedDatabase> CloneDatabaseAsync() => ShipLeanDatabase.CloneSeedAsync();
}
