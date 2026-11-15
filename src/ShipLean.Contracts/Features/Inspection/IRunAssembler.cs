namespace ShipLean.Contracts.Inspection;

// Pure, no I/O (K4).
public interface IRunAssembler
{
    AssembledRun Assemble(RawRun raw);
}
