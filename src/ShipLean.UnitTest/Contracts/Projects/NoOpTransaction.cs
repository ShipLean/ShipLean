using System.Data;
using System.Data.Common;

namespace ShipLean.UnitTest.Contracts.Projects;

// What the fakes' contract hooks hand to methods that take a transaction: nothing to commit. Joining a real transaction is
// asserted only by the Postgres derivations.
internal sealed class NoOpTransaction : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection? DbConnection => null;

    public override void Commit()
    {
    }

    public override void Rollback()
    {
    }
}
