/** "2026 Q4 Sprint 3" is the same 12 characters for every sprint of a year, so a column cut at the end shows only "2026 …". The short form "2026 Q4 S3" keeps what tells them apart; any other name is kept whole. */
export function shortSprintName(name: string): string {
  return name.replace(/^(\d{4} Q\d) Sprint (\d+)$/, '$1 S$2');
}
