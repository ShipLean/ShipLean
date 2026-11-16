namespace ShipLean.Contracts.Inspection;

// Whether this instance has Inspection on (SHIPLEAN_INSPECTION).
public interface IInspectionStatus
{
    bool Enabled { get; }
}
