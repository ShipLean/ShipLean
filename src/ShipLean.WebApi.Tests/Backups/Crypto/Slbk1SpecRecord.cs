namespace ShipLean.WebApi.Tests.Backups.Crypto;

// One record of an SLBK1 file, as Slbk1Spec reads it: where it starts, its flags and its plaintext length.
public sealed record Slbk1SpecRecord(int Offset, byte Flags, int Length)
{
    // Flags, length, ciphertext and tag.
    public int Size => 5 + Length + Slbk1Spec.TagLength;

    public int End => Offset + Size;
}
