namespace ShipLean.Contracts.Views.InspectionMetrics;

// GET of a project's Inspection lead time, and the MCP tool inspection_metrics's result (K2), following the design's INS.build and
// toJson. Instants are UTC; hours are rounded to one decimal on the server; every label is made on the server in the viewer's zone.
// Headline is the last finished sprint with the running one as so far (item D1). Quarters sum every sprint of each quarter the
// shown sprints fall in (design D7). Cuts is null when Cut is None. Longest is the range's slowest 6 tasks. LeftOut lists the tasks
// closed with no start recorded (design D9). PeriodDetail is null unless the query names a period. Quality (QK3) is the escaping
// bugs over the shown sprints; it, and every other Quality and Bugs in the view, is null when the project has no bugs at all.
public sealed record InspectionMetricsViewDto(
    InspectionProjectDto Project,
    InspectionCadence Cadence,
    InspectionRange Range,
    InspectionCut Cut,
    InspectionAggregate Aggregate,
    DateTimeOffset GeneratedAt,
    string Caveat,
    InspectionCoverageDto Coverage,
    InspectionHeadlineDto Headline,
    IReadOnlyList<InspectionPeriodDto> Periods,
    IReadOnlyList<InspectionQuarterDto> Quarters,
    IReadOnlyList<InspectionCutRowDto>? Cuts,
    IReadOnlyList<InspectionTaskDto> Longest,
    IReadOnlyList<InspectionLeftOutDto> LeftOut,
    InspectionPeriodDetailDto? PeriodDetail,
    InspectionQualityStatsDto? Quality);
