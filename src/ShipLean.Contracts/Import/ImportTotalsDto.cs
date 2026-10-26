namespace ShipLean.Contracts.Import;

// What the run would bring in under the current choices (the strip above the mapping): a switched-off comments, attachments or changelog option counts 0. People: the Jira users it would map.
public sealed record ImportTotalsDto(int Issues, int Comments, int Attachments, int Revisions, int Sprints, int People);
