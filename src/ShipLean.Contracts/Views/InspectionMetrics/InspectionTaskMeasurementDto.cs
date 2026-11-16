namespace ShipLean.Contracts.Views.InspectionMetrics;

// How one task's lead time was measured, for the rail: its segments in time order and its events. Hours offsets run from the
// task's start.
public sealed record InspectionTaskMeasurementDto(
    InspectionTaskDto Task,
    IReadOnlyList<InspectionSegmentDto> Segments,
    IReadOnlyList<InspectionEventDto> Events);
