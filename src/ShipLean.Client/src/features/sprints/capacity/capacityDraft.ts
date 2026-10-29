import type { MemberDto } from '../../../api/projects';
import type { SprintCapacityDto, SprintCapacityPut } from '../../../api/sprints';

/**
 * What the form holds for one member: whether they are in the table, the text of the days input (empty means the
 * sprint's default days) and the note.
 */
export interface DraftRow {
  present: boolean;
  days: string;
  note: string;
}

/** By member id. A member missing from it is not in the table. */
export type CapacityDraft = Readonly<Record<string, DraftRow>>;

/** The largest value the server accepts for velocity and for one member's points. */
export const LARGEST_CAPACITY = 9999.99;

const BLANK: DraftRow = { present: false, days: '', note: '' };

export const rowOf = (draft: CapacityDraft, userId: string): DraftRow => draft[userId] ?? BLANK;

/** The sprint's calendar as the capacity maths needs it. */
export interface CapacityShape {
  /** W: the sprint's working days, holidays not removed. */
  workingDays: number;
  /** W minus the holidays: what a member who typed nothing gets. */
  defaultDays: number;
}

export const shapeOf = (capacity: SprintCapacityDto): CapacityShape => ({
  workingDays: capacity.workingDays,
  defaultDays: capacity.defaultDays,
});

/** The entered rows as form text. Typed days above `workingDays` (when given) come down to it, as a copy into a shorter sprint needs. */
export function draftOf(capacity: SprintCapacityDto, workingDays?: number): CapacityDraft {
  return Object.fromEntries(
    capacity.members.map((m) => [
      m.user.id,
      {
        present: true,
        days:
          m.days === null
            ? ''
            : `${workingDays === undefined ? m.days : Math.min(m.days, workingDays)}`,
        note: m.note ?? '',
      },
    ]),
  );
}

/** A row the user changed, and the server's row it was changed from. */
export interface TouchedRow {
  row: DraftRow;
  base: DraftRow;
}

/** By member id: only the rows the user changed. Everything else is whatever the server holds now. */
export type TouchedRows = Readonly<Record<string, TouchedRow>>;

const sameRow = (a: DraftRow, b: DraftRow) =>
  a.present === b.present && a.days === b.days && a.note === b.note;

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

/** The project members who are in the table, in the members' order. Rows of people who left the project are not shown or saved. */
export const tableMembers = (members: MemberDto[], draft: CapacityDraft): MemberDto[] =>
  members.filter((member) => rowOf(draft, member.user.id).present);

/** Rounds away float noise from sums and differences of points (two decimals, as the server keeps them). */
export const roundPoints = (value: number) => Math.round(value * 100) / 100;

/** What the velocity box holds when nobody has typed in it: the stored value, else the default for a sprint with no rows. An old points-only sprint (rows, no velocity) starts empty. */
export function velocityTextOf(capacity: SprintCapacityDto): string {
  if (capacity.velocity !== null) return `${capacity.velocity}`;
  return capacity.members.length === 0 ? `${capacity.velocityDefault}` : '';
}

/** The velocity typed, or undefined when it is empty, not a number, negative, too large or has more than two decimals. */
export function parseVelocity(text: string): number | undefined {
  const trimmed = text.trim();
  if (trimmed === '') return undefined;
  const value = Number(trimmed);
  return Number.isFinite(value) &&
    value >= 0 &&
    value <= LARGEST_CAPACITY &&
    roundPoints(value) === value
    ? value
    : undefined;
}

/** The days typed: null for empty (the default), NaN for text that is not a valid number of days for the sprint. */
export function parseDays(text: string, workingDays: number): number | null {
  const trimmed = text.trim();
  if (trimmed === '') return null;
  const value = Number(trimmed);
  const valid =
    Number.isFinite(value) && value >= 0 && value <= workingDays && Number.isInteger(value * 2);
  return valid ? value : Number.NaN;
}

/**
 * A member's capacity: velocity / members * days / workingDays, where days is the typed value or the default, never more
 * than the working days. Two decimals, half away from zero, at most 9999.99. Zero working days or no members give 0.
 * The server uses the same formula, so what the table shows is what a save stores.
 */
export function memberPoints(
  velocity: number,
  members: number,
  days: number | null,
  shape: CapacityShape,
): number {
  const { workingDays, defaultDays } = shape;
  if (workingDays === 0 || members === 0) return 0;
  const effectiveDays = Math.min(days ?? defaultDays, workingDays);
  // Whole numbers throughout, so the half-cent cases round as the server's decimal does (velocity has at most two
  // decimals, days are halves): points = v100 * d2 / (2 * members * workingDays) hundredths, half up.
  const v100 = Math.round(velocity * 100);
  const d2 = Math.round(effectiveDays * 2);
  const den = 2 * members * workingDays;
  const points = Math.floor((2 * v100 * d2 + den) / (2 * den)) / 100;
  return Math.min(points, LARGEST_CAPACITY);
}

/** The days a row counts as: what was typed, else the default. Undefined when the text is not valid. */
export function effectiveDaysOf(row: DraftRow, shape: CapacityShape): number | undefined {
  const days = parseDays(row.days, shape.workingDays);
  if (days === null) return Math.min(shape.defaultDays, shape.workingDays);
  return Number.isNaN(days) ? undefined : Math.min(days, shape.workingDays);
}

/** The team total as it stands in the form, each member's points (undefined for a row that cannot be counted yet), and whether any row is in the table. */
export function draftTotals(
  members: MemberDto[],
  draft: CapacityDraft,
  velocity: number | undefined,
  shape: CapacityShape,
): { total: number; entered: boolean; points: Readonly<Record<string, number | undefined>> } {
  const rows = tableMembers(members, draft);
  const points: Record<string, number | undefined> = {};
  let total = 0;
  for (const member of rows) {
    const days = parseDays(rowOf(draft, member.user.id).days, shape.workingDays);
    const value =
      velocity === undefined || Number.isNaN(days)
        ? undefined
        : memberPoints(velocity, rows.length, days, shape);
    points[member.user.id] = value;
    total += value ?? 0;
  }
  return { total: roundPoints(total), entered: rows.length > 0, points };
}

const VELOCITY_ERROR = 'Velocity is zero or more points, up to 9999.99, with at most two decimals.';

/** The PUT body, or the first problem in the form. With no rows the velocity is sent only when it is valid, so a Clear leaves the stored one alone. */
export function toPut(
  members: MemberDto[],
  draft: CapacityDraft,
  velocityText: string,
  shape: CapacityShape,
): { put: SprintCapacityPut } | { error: string } {
  const rows = tableMembers(members, draft);
  const velocity = parseVelocity(velocityText);
  if (rows.length > 0 && velocityText.trim() === '') return { error: 'Capacity needs a velocity.' };
  if (velocity === undefined && velocityText.trim() !== '') return { error: VELOCITY_ERROR };
  const put: SprintCapacityPut['members'] = [];
  for (const member of rows) {
    const row = rowOf(draft, member.user.id);
    const days = parseDays(row.days, shape.workingDays);
    if (Number.isNaN(days)) {
      return {
        error: `${member.user.name}: days are between 0 and ${shape.workingDays}, in steps of half a day.`,
      };
    }
    const note = row.note.trim();
    put.push({ userId: member.user.id, days, note: note === '' ? null : note });
  }
  return { put: { velocity: velocity ?? null, members: put } };
}
