namespace ShipLean.Contracts.Inspection;

// One task's share of its runs' tokens (CK1): the runs counted toward it, the tokens in and out it was given, and the models those
// runs used, ordered and distinct.
public sealed record TaskTokens(int Runs, long TokensIn, long TokensOut, IReadOnlyList<string> Models)
{
    public long Total => TokensIn + TokensOut;
}
