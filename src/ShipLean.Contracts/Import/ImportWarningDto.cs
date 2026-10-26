namespace ShipLean.Contracts.Import;

public sealed record ImportWarningDto(ImportWarningSeverity Severity, ImportMappingSection Section, string Message);
