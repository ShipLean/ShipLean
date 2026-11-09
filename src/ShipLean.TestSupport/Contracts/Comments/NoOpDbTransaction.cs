using System.Data;
using System.Data.Common;

namespace ShipLean.TestSupport.Contracts.Comments;

// A DbTransaction for stores that have no database (the in-memory ones): every member does nothing.
public sealed class NoOpDbTransaction : DbTransaction
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
