using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Harness;

// Base class for a scenario that starts from the bare schema (no seed rows): the first-run experience, or a test that
// builds exactly the data it needs. Same isolation as SeededAcceptanceTest.
public abstract class EmptyAcceptanceTest : AcceptanceFixture
{
    // The bare schema has no users, so nobody is signed in until the test inserts one and calls SignInAsync(email, password).
    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Task<ClonedDatabase> CloneDatabaseAsync() => ShipLeanDatabase.CloneEmptyAsync();
}
