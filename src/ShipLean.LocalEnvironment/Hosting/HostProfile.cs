namespace ShipLean.LocalEnvironment.Hosting;

// Which background services stay in an AppHost. Both profiles keep everything that would leave the machine switched off.
public enum HostProfile
{
    // The tests: no background indexing, so the exact-count pass tests and the rows builders arrange are not raced.
    Test,

    // A person or an agent running the app: the search indexer stays, so search works against the seeded data.
    Local,
}
