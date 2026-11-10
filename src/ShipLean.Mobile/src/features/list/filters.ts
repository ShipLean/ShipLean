import type {
  FeatureGroupDto,
  FeatureRowDto,
  FeaturesOverviewDto,
} from '@shiplean/core/api/featuresOverview';
import type { InboxItemDto } from '@shiplean/core/api/inbox';
import type { RollupCountDto } from '@shiplean/core/api/views';
import type { ParentRefDto } from '@shiplean/core/api/workItems';

export type FeatureChip = 'all' | 'needsYou' | 'atRisk' | 'blocked';

const CHIPS: readonly unknown[] = ['all', 'needsYou', 'atRisk', 'blocked'];

export interface FeatureFilter {
  chip: FeatureChip;
  // The Epic's id, or null for every Epic.
  epicId: string | null;
}

export const NO_FILTER: FeatureFilter = { chip: 'all', epicId: null };

export const isFiltered = (filter: FeatureFilter): boolean =>
  filter.chip !== 'all' || filter.epicId !== null;

// What a voice screen view can turn on: `command: 'filter'` with the chip's name in args.chip.
export type FeatureViewArgs = { chip: FeatureChip };

export function chipFromView(
  command: string,
  args: { [K in keyof FeatureViewArgs]?: unknown },
): FeatureChip | null {
  return command === 'filter' && CHIPS.includes(args.chip) ? (args.chip as FeatureChip) : null;
}

const matchesChip = (row: FeatureRowDto, chip: FeatureChip): boolean => {
  switch (chip) {
    case 'all':
      return true;
    case 'needsYou':
      return row.needsYou !== null;
    case 'atRisk':
      return row.health === 'atRisk';
    case 'blocked':
      return row.health === 'blocked';
  }
};

export interface ShownGroup {
  group: FeatureGroupDto;
  rows: FeatureRowDto[];
  // The server's count with no filter, otherwise the rows that match.
  count: number;
}

// The groups in the server's order. A filter applies to the rows the server sent: a folded group sent none, so it
// goes while a filter is on, and so does a group nothing matches.
export function shownGroups(overview: FeaturesOverviewDto, filter: FeatureFilter): ShownGroup[] {
  if (!isFiltered(filter)) {
    return overview.groups.map((group) => ({ group, rows: group.rows, count: group.count }));
  }
  const shown: ShownGroup[] = [];
  for (const group of overview.groups) {
    if (group.folded) continue;
    const rows = group.rows.filter(
      (row) =>
        matchesChip(row, filter.chip) && (filter.epicId === null || row.epic?.id === filter.epicId),
    );
    if (rows.length > 0) shown.push({ group, rows, count: rows.length });
  }
  return shown;
}

export interface ChipCounts {
  all: number;
  needsYou: number;
  atRisk: number;
  blocked: number;
}

// All is the server's count of every Feature, Closed ones included; the others count the rows the server sent.
export function chipCounts(overview: FeaturesOverviewDto): ChipCounts {
  const rows = overview.groups.flatMap((g) => g.rows);
  return {
    all: overview.groups.reduce((sum, g) => sum + g.count, 0),
    needsYou: rows.filter((r) => r.needsYou !== null).length,
    atRisk: rows.filter((r) => r.health === 'atRisk').length,
    blocked: rows.filter((r) => r.health === 'blocked').length,
  };
}

// The distinct Epics the Features sit under, in the order they first appear.
export function epicsOf(overview: FeaturesOverviewDto): ParentRefDto[] {
  const epics = new Map<string, ParentRefDto>();
  for (const row of overview.groups.flatMap((g) => g.rows)) {
    if (row.epic !== null && !epics.has(row.epic.id)) epics.set(row.epic.id, row.epic);
  }
  return [...epics.values()];
}

export interface NeedsGroup {
  feature: FeatureRowDto;
  items: InboxItemDto[];
}

// Inbox items under their Feature's header: the Feature's own asks first, then its Stories' (underKey), each in the
// Inbox's order. An item that is neither a Feature of the rows nor under one is left out.
export function groupInboxByFeature(
  features: FeatureRowDto[],
  items: InboxItemDto[],
): NeedsGroup[] {
  const groups: NeedsGroup[] = [];
  for (const feature of features) {
    const own = items.filter((i) => i.key === feature.key);
    const under = items.filter((i) => i.underKey === feature.key);
    if (own.length + under.length > 0) groups.push({ feature, items: [...own, ...under] });
  }
  return groups;
}

export type RankMove = { from: number; up: boolean };

export const ranksOf = (rows: FeatureRowDto[]): Map<string, number> =>
  new Map(rows.map((row) => [row.key, row.rank]));

// The rows that moved since the last read, by key, with the rank they came from: the rows outside the longest run of
// rows that kept their relative order (one row for a single move). Of two equal runs the earlier one is kept. A row not
// in the last read isn't a move.
export function rankMoves(
  last: ReadonlyMap<string, number>,
  rows: FeatureRowDto[],
): Map<string, RankMove> {
  const known = rows.flatMap((row) => {
    const was = last.get(row.key);
    return was === undefined ? [] : [{ key: row.key, before: was, now: row.rank }];
  });
  // Longest strictly increasing run of old ranks, in the new order.
  const length: number[] = [];
  const link: number[] = [];
  for (let i = 0; i < known.length; i++) {
    length[i] = 1;
    link[i] = -1;
    for (let j = 0; j < i; j++) {
      if (known[j]!.before < known[i]!.before && length[j]! + 1 > length[i]!) {
        length[i] = length[j]! + 1;
        link[i] = j;
      }
    }
  }
  const kept = new Set<string>();
  let at = length.reduce((best, len, i) => (len > length[best]! ? i : best), 0);
  for (; known.length > 0 && at >= 0; at = link[at]!) kept.add(known[at]!.key);
  const moves = new Map<string, RankMove>();
  for (const { key, before, now } of known) {
    if (!kept.has(key)) moves.set(key, { from: before, up: now < before });
  }
  return moves;
}

const NOUNS: Partial<Record<RollupCountDto['type'], [string, string]>> = {
  story: ['story', 'stories'],
  bug: ['bug', 'bugs'],
  action: ['action item', 'action items'],
  feature: ['nested Feature', 'nested Features'],
};

// "2/9 stories · 0/1 bug": what is closed of what is under the Feature, by type. Empty when nothing is.
export function rollupText(rollups: RollupCountDto[]): string {
  const parts: string[] = [];
  for (const [type, noun] of Object.entries(NOUNS)) {
    const entry = rollups.find((r) => r.type === type);
    if (entry === undefined) continue;
    const total = entry.open + entry.closed;
    if (total > 0) parts.push(`${entry.closed}/${total} ${total === 1 ? noun[0] : noun[1]}`);
  }
  return parts.join(' · ');
}

// Who wrote the status line and how long ago: a person's name or "‹agent› (agent)", then the server's age label.
export function statusBy(row: FeatureRowDto): string {
  const verified = row.statusVerified;
  if (verified === null) return '';
  const who = verified.agent === null ? (verified.by?.name ?? '') : `${verified.agent} (agent)`;
  return who === '' ? verified.atLabel : `${who} · ${verified.atLabel}`;
}
