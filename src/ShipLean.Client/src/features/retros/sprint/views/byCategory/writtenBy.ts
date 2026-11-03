import type { RetroPointDto } from '../../../../../api/retros';

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
