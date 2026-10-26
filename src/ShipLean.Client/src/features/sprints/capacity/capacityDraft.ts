import type { MemberDto } from '../../../api/projects';
import type { MemberCapacityPut, SprintCapacityDto } from '../../../api/sprints';

/** What the form holds for one member: the text of the two inputs. Empty points means no entry. */
export interface DraftRow {
  points: string;
  note: string;
}

/** By member id. A member missing from it is a blank row. */
export type CapacityDraft = Readonly<Record<string, DraftRow>>;

/** The largest value the server accepts for one member. */
export const LARGEST_CAPACITY = 9999.99;

const BLANK: DraftRow = { points: '', note: '' };

export const rowOf = (draft: CapacityDraft, userId: string): DraftRow => draft[userId] ?? BLANK;

/** The entered capacity as form text: members without an entry stay blank. */
export function draftOf(capacity: SprintCapacityDto): CapacityDraft {
  return Object.fromEntries(
    capacity.members.map((m) => [m.user.id, { points: `${m.points}`, note: m.note ?? '' }]),
  );
}

/** A row the user changed, and the server's row it was changed from. */
export interface TouchedRow {
  row: DraftRow;
  base: DraftRow;
}

/** By member id: only the rows the user changed. Everything else is whatever the server holds now. */
export type TouchedRows = Readonly<Record<string, TouchedRow>>;

const sameRow = (a: DraftRow, b: DraftRow) => a.points === b.points && a.note === b.note;

/** The server's rows with the user's changed rows laid over them: what the table shows and what a save sends. */
export function layOver(server: CapacityDraft, touched: TouchedRows): CapacityDraft {
  return {
    ...server,
    ...Object.fromEntries(Object.entries(touched).map(([userId, t]) => [userId, t.row])),
  };
}

/** Records one changed row; its base stays the server row it first departed from. */
export function touchRow(
  touched: TouchedRows,
  server: CapacityDraft,
  userId: string,
  row: DraftRow,
): TouchedRows {
  return { ...touched, [userId]: { row, base: touched[userId]?.base ?? rowOf(server, userId) } };
}

/** Copy and Clear change every member's row: `next` is the whole form they produce. */
export function touchAll(
  touched: TouchedRows,
  server: CapacityDraft,
  members: MemberDto[],
  next: CapacityDraft,
): TouchedRows {
  return members.reduce(
    (all, member) => touchRow(all, server, member.user.id, rowOf(next, member.user.id)),
    touched,
  );
}

/** The members whose row the user changed and someone else has since changed on the server too. */
export function changedUnderneath(
  members: MemberDto[],
  server: CapacityDraft,
  touched: TouchedRows,
): MemberDto[] {
  return members.filter((member) => {
    const t = touched[member.user.id];
    return t !== undefined && !sameRow(t.base, rowOf(server, member.user.id));
  });
}

/** Rounds away float noise from sums and differences of points (two decimals, as the server keeps them). */
export const roundPoints = (value: number) => Math.round(value * 100) / 100;

const parsePoints = (text: string): number | undefined => {
  const trimmed = text.trim();
  if (trimmed === '') return undefined;
  const value = Number(trimmed);
  return Number.isFinite(value) ? value : Number.NaN;
};

const isValid = (value: number) =>
  value >= 0 && value <= LARGEST_CAPACITY && roundPoints(value) === value;

/** The team total as it stands in the form: the sum of the valid entries of the members (the rows on screen, as `toPut` walks them), and whether any row has one. */
export function draftTotal(
  members: MemberDto[],
  draft: CapacityDraft,
): { total: number; entered: boolean } {
  let total = 0;
  let entered = false;
  for (const member of members) {
    const value = parsePoints(rowOf(draft, member.user.id).points);
    if (value === undefined) continue;
    entered = true;
    if (isValid(value)) total += value;
  }
  return { total: roundPoints(total), entered };
}

/** The PUT body: one entry per member with points, or the first problem in the form. */
export function toPut(
  members: MemberDto[],
  draft: CapacityDraft,
): { put: MemberCapacityPut[] } | { error: string } {
  const put: MemberCapacityPut[] = [];
  for (const member of members) {
    const row = rowOf(draft, member.user.id);
    const points = parsePoints(row.points);
    if (points === undefined) continue;
    if (!isValid(points)) {
      return {
        error: `${member.user.name}: capacity is zero or more points, with at most two decimals.`,
      };
    }
    const note = row.note.trim();
    put.push({ userId: member.user.id, points, note: note === '' ? null : note });
  }
  return { put };
}
