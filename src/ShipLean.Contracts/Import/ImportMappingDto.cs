namespace ShipLean.Contracts.Import;

// The mapping reviewed before a run. Totals and Warnings are computed by the server and ignored on a request.
public sealed record ImportMappingDto(IReadOnlyList<IssueTypeMappingDto> IssueTypes, IReadOnlyList<StatusMappingDto> Statuses, IReadOnlyList<UserMappingDto> Users, ImportFieldsDto Fields, ImportTotalsDto Totals, IReadOnlyList<ImportWarningDto> Warnings);
