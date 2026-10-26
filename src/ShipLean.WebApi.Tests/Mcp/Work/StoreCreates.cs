namespace ShipLean.WebApi.Tests.Mcp.Work;

// What RecordingWorkItemStore saw: single creates, and the size of each CreateManyAsync batch.
public sealed class StoreCreates
{
    private readonly List<int> _batches = [];
    private int _creates;

    public int Creates => _creates;

    public IReadOnlyList<int> CreateManySizes
    {
        get
        {
            lock (_batches)
            {
                return [.. _batches];
            }
        }
    }

    public void NoteCreate() => Interlocked.Increment(ref _creates);

    public void NoteCreateMany(int size)
    {
        lock (_batches)
        {
            _batches.Add(size);
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _creates, 0);
        lock (_batches)
        {
            _batches.Clear();
        }
    }
}
