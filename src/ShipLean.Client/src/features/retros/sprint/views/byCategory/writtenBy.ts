import type { RetroCategoryColumnDto, RetroPointDto } from '../../../../../api/retros';

export type WrittenBy = 'all' | 'agents' | 'people';

export const WRITTEN_BY_OPTIONS: { id: WrittenBy; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'agents', label: 'Agents' },
  { id: 'people', label: 'People' },
];

/** Whether the Written by filter lets this point through: People are `person` authors, Agents are conductors and sub-agents. */
export function matchesWrittenBy(point: RetroPointDto, writtenBy: WrittenBy): boolean {
  if (writtenBy === 'all') return true;
  const person = point.author.role === 'person';
  return writtenBy === 'people' ? person : !person;
}

/** The points the filter lets through, order kept. Shared by every view that offers Written by. */
export function filterPoints(points: RetroPointDto[], writtenBy: WrittenBy): RetroPointDto[] {
  return writtenBy === 'all' ? points : points.filter((p) => matchesWrittenBy(p, writtenBy));
}

/** The columns with only the points the filter lets through; a theme group left with no points goes. Order is kept. */
export function filterColumns(
  columns: RetroCategoryColumnDto[],
  writtenBy: WrittenBy,
): RetroCategoryColumnDto[] {
  if (writtenBy === 'all') return columns;
  return columns.map((column) => ({
    ...column,
    groups: column.groups
      .map((group) => ({ ...group, points: filterPoints(group.points, writtenBy) }))
      .filter((group) => group.points.length > 0),
  }));
}
