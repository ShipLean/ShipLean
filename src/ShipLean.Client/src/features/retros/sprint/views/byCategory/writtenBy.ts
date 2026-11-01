import type { RetroCategoryColumnDto, RetroPointDto } from '../../../../../api/retros';

export type WrittenBy = 'all' | 'agents' | 'people';

export const WRITTEN_BY_OPTIONS: { id: WrittenBy; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'agents', label: 'Agents' },
  { id: 'people', label: 'People' },
];

function matches(point: RetroPointDto, writtenBy: WrittenBy): boolean {
  if (writtenBy === 'all') return true;
  const person = point.author.role === 'person';
  return writtenBy === 'people' ? person : !person;
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
      .map((group) => ({ ...group, points: group.points.filter((p) => matches(p, writtenBy)) }))
      .filter((group) => group.points.length > 0),
  }));
}
