import type { BacklogFilter, BacklogLevel } from '@shiplean/core/api/backlog';
import type { CriteriaJson } from '@shiplean/core/api/persisted';
import type { NeedsYouDto } from '@shiplean/core/api/screens';
import type { BacklogRowDto } from '@shiplean/core/api/views';

export interface BacklogChips {
  // Applied to the rows the server returned; never changes rank numbers.
  needsMe: boolean;
  // On reads only open items (includeCompleted=false).
  open: boolean;
  // The id of the Epic (Features level) or Feature (Stories level) the list is narrowed to.
  parentId: string | null;
}

// The criteria field the parent chip narrows by at a level: Features by Epic, Stories by Feature.
export function parentField(level: BacklogLevel): 'epic' | 'parent' | null {
  if (level === 'features') return 'epic';
  if (level === 'stories') return 'parent';
  return null;
}

// The level whose rows the parent chip picks from.
export function pickerLevel(level: BacklogLevel): BacklogLevel | null {
  if (level === 'features') return 'epics';
  if (level === 'stories') return 'features';
  return null;
}

// The `filter` query value: the criteria JSON, or '' when nothing narrows the list (the web's filterParamOf).
export function criteriaParam(level: BacklogLevel, parentId: string | null): string {
  const field = parentField(level);
  if (field === null || parentId === null) return '';
  const criteria: CriteriaJson = { match: 'all', clauses: [{ field, op: '=', value: parentId }] };
  return JSON.stringify(criteria);
}

export function backlogFilter(level: BacklogLevel, chips: BacklogChips): BacklogFilter {
  return {
    mode: 'list',
    filter: criteriaParam(level, chips.parentId),
    includeCompleted: !chips.open,
  };
}

// A criteria filter narrows the list, so reordering is off (the web's isFiltering).
export const isFiltering = (level: BacklogLevel, parentId: string | null): boolean =>
  criteriaParam(level, parentId) !== '';

// The level a link opens at: the one it names, else the one its filter belongs to, else Features.
export function openingLevel(
  level: BacklogLevel | undefined,
  epic: string | undefined,
  feature: string | undefined,
): BacklogLevel {
  if (level !== undefined) return level;
  if (feature !== undefined) return 'stories';
  if (epic !== undefined) return 'features';
  return 'features';
}

// The rows that wait on the person, with their own rank numbers.
export const needsMeRows = (rows: BacklogRowDto[]): BacklogRowDto[] =>
  rows.filter((row) => row.needsYou !== null);

const plural = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`;

// "2 decisions · 1 question", leaving out a kind that has none.
export function needsLine(needs: NeedsYouDto): string {
  const parts: string[] = [];
  if (needs.decisions > 0) parts.push(plural(needs.decisions, 'decision'));
  if (needs.questions > 0) parts.push(plural(needs.questions, 'question'));
  return parts.join(' · ');
}

export type RankMove = { from: number; up: boolean };

// The rows whose rank differs from the last read, by key. A row not in the last read isn't a move.
export function rankMoves(last: ReadonlyMap<string, number>, rows: BacklogRowDto[]) {
  const moves = new Map<string, RankMove>();
  for (const row of rows) {
    const before = last.get(row.key);
    if (before === undefined || row.rank === null || before === row.rank) continue;
    moves.set(row.key, { from: before, up: row.rank < before });
  }
  return moves;
}

export function ranksOf(rows: BacklogRowDto[]): Map<string, number> {
  const ranks = new Map<string, number>();
  for (const row of rows) if (row.rank !== null) ranks.set(row.key, row.rank);
  return ranks;
}

export type BacklogChip = 'needsMe' | 'open' | 'epic' | 'feature';

export type ViewChange =
  | { kind: 'level'; level: BacklogLevel }
  | { kind: 'chip'; chip: BacklogChip; on: boolean; key: string | null };

const LEVELS: readonly unknown[] = ['epics', 'features', 'stories'];
const CHIPS: readonly unknown[] = ['needsMe', 'open', 'epic', 'feature'];

// Reads the screen view intents the Backlog's voice spec returns:
//   command 'level'  args { level: 'epics' | 'features' | 'stories' }
//   command 'filter' args { chip: 'needsMe' | 'open' | 'epic' | 'feature', on?: boolean (default true), key?: string }
// `key` names the Epic or Feature of the epic and feature chips. Anything else is not a change.
export function viewChange(command: string, args: Record<string, unknown>): ViewChange | null {
  if (command === 'level') {
    return LEVELS.includes(args.level)
      ? { kind: 'level', level: args.level as BacklogLevel }
      : null;
  }
  if (command === 'filter' && CHIPS.includes(args.chip)) {
    return {
      kind: 'chip',
      chip: args.chip as BacklogChip,
      on: args.on !== false,
      key: typeof args.key === 'string' ? args.key : null,
    };
  }
  return null;
}
