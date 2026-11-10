import type { AskEventKind, AskHistoryLineDto } from '@shiplean/core/api/asks';
import type { CommentDto } from '@shiplean/core/api/comments';
import { FIELD_IDS } from '@shiplean/core/api/persisted';
import type { FieldChange } from '@shiplean/core/api/persisted';
import type { HistoryDto, RevisionDto } from '@shiplean/core/api/workItems';

export type TimelineFilter = 'all' | 'notes' | 'changes';

export type TimelineEntry =
  | { kind: 'note'; id: string; at: string; comment: CommentDto }
  | { kind: 'revision'; id: string; at: string; revision: RevisionDto }
  | { kind: 'ask'; id: string; at: string; line: AskHistoryLineDto };

// Notes are comments; changes are revisions and ask events.
export const isChange = (entry: TimelineEntry): boolean => entry.kind !== 'note';

const rank = { note: 0, revision: 1, ask: 2 } as const;

// One list, newest first: comments by createdAt, revisions by changedAt, ask events by at. A tie keeps comments first, then revisions,
// then ask events, each in the order the server sent them. Either half may be missing (its read failed or is still loading).
export function mergeTimeline(
  comments: readonly CommentDto[] | undefined,
  history: HistoryDto | undefined,
): TimelineEntry[] {
  const entries: TimelineEntry[] = [
    ...(comments ?? []).map((comment): TimelineEntry => ({
      kind: 'note',
      id: comment.id,
      at: comment.createdAt,
      comment,
    })),
    ...(history?.revisions ?? []).map((revision): TimelineEntry => ({
      kind: 'revision',
      id: `rev-${revision.rev}`,
      at: revision.changedAt,
      revision,
    })),
    ...(history?.asks ?? []).map((line): TimelineEntry => ({
      kind: 'ask',
      id: `ask-${line.kind}-${line.badge}-${line.at}`,
      at: line.at,
      line,
    })),
  ];
  return entries
    .map((entry, order) => ({ entry, order, time: Date.parse(entry.at) }))
    .sort((a, b) => b.time - a.time || rank[a.entry.kind] - rank[b.entry.kind] || a.order - b.order)
    .map(({ entry }) => entry);
}

export function filterTimeline(
  entries: readonly TimelineEntry[],
  filter: TimelineFilter,
): TimelineEntry[] {
  if (filter === 'all') return [...entries];
  return entries.filter((entry) => isChange(entry) === (filter === 'changes'));
}

// The app calls the assignee the Owner (the web's field list says "Assigned to").
const FIELD_NAMES: Record<string, string> = { assignee: 'Owner' };

const fieldName = (field: string): string =>
  FIELD_NAMES[field] ?? FIELD_IDS.find((definition) => definition.id === field)?.label ?? field;

const SHORT = 60;
const NONE = '—';

// A value that can be shown as it is: a number, a short string, or a list of short strings. Anything else is undefined.
function plain(value: FieldChange['from']): string | undefined {
  if (value === null) return NONE;
  if (typeof value === 'number') return String(value);
  if (typeof value === 'string') return value.length <= SHORT ? value : undefined;
  if (Array.isArray(value) && value.every((entry) => typeof entry === 'string')) {
    const joined = value.length === 0 ? NONE : value.join(', ');
    return joined.length <= SHORT ? joined : undefined;
  }
  return undefined;
}

// "State New → Active": the server's labels where it sent them, otherwise the values when they are short. Long text, checklists and
// links have nothing short to show and read "changed", as the web's revision rail words them.
export function changeLine(change: FieldChange): string {
  const name = fieldName(change.field);
  if (change.toLabel !== null) return `${name} ${change.fromLabel ?? NONE} → ${change.toLabel}`;
  const from = plain(change.from);
  const to = plain(change.to);
  return from !== undefined && to !== undefined ? `${name} ${from} → ${to}` : `${name} changed`;
}

const cut = (text: string): string => (text.length > SHORT ? `${text.slice(0, SHORT - 2)}…` : text);

const VERBS: Record<AskEventKind, string> = {
  raised: 'raised',
  edited: 'edited',
  confirmed: 'approved',
  changed: 'changed',
  withdrawn: 'withdrawn',
  asked: 'asked',
  answered: 'answered',
};

// "D10 approved"; an answer or a changed answer follows, "D3 changed Yes → No".
export function askLine(line: AskHistoryLineDto): string {
  const head = `${line.badge} ${VERBS[line.kind]}`;
  if (line.to === null) return head;
  const before = line.kind === 'changed' && line.from !== null ? `${cut(line.from)} → ` : '';
  return `${head} ${before}${cut(line.to)}`;
}
