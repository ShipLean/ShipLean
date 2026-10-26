using System.Data;
using System.Data.Common;

namespace ShipLean.Application.Fakes.WorkItems;

// What the fake store hands the InTransaction hook: no connection, commit and rollback do nothing. A hook that needs
// transaction.Connection only works against Postgres.
internal sealed class InMemoryTransaction : DbTransaction
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
