using Agoda.IoC.Core;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection;

[RegisterSingleton]
public sealed class InspectionStatus(InspectionOptions options) : IInspectionStatus
{
    public bool Enabled => options.Enabled;
}
