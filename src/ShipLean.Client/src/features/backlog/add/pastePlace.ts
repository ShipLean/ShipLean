import type { BacklogAddRequest } from '@shiplean/core/api/backlogWrites';
import type { SprintRef } from '@shiplean/core/api/refs';
import type { BacklogRowDto, BacklogViewDto } from '@shiplean/core/api/views';

/** The "Put them" choices: the top, the bottom, just below the focused row, or the top of a future sprint. */
export interface PlaceOption {
  value: string;
  label: string;
}

export interface PlaceContext {
  rows: readonly BacklogRowDto[];
  /** The future sprints the backlog view shows, in the order it shows them. */
  sprints: readonly SprintRef[];
  focused: BacklogRowDto | null;
  /** The shown row ids in rank order, to find the row after the focused one. */
  order: readonly string[];
  /** Planning: the focused row's own section, since each section is the one stack filtered by sprint. */
  sectionRows: readonly BacklogRowDto[] | null;
}

const TOP = 'top';
const BOTTOM = 'bottom';
const BELOW = 'below';
const SPRINT = 'sprint:';

/** The rows and future sprints of a backlog view, whichever mode it was read in. */
export function placeContext(
  view: BacklogViewDto | undefined,
  focusedId: string | null,
  order: readonly string[],
): PlaceContext {
  const rows = view === undefined ? [] : [...view.rows, ...view.sections.flatMap((s) => s.rows)];
  const refs = view === undefined ? [] : view.sections.map((s) => s.sprint);
  const seen = new Map<string, SprintRef>();
  for (const ref of [...refs, ...rows.map((row) => row.sprint)])
    if (ref !== null && ref.status === 'future' && !seen.has(ref.id)) seen.set(ref.id, ref);
  // The shown order put back in rank order: a list sorted by a column shows rows out of rank, and "below" means in rank.
  const rankOf = new Map(rows.map((row) => [row.id, row.rank ?? Number.MAX_SAFE_INTEGER]));
  const rank = (id: string) => rankOf.get(id) ?? Number.MAX_SAFE_INTEGER;
  return {
    rows,
    sprints: [...seen.values()],
    focused: rows.find((row) => row.id === focusedId) ?? null,
    order: [...order].sort((a, b) => rank(a) - rank(b)),
    sectionRows:
      view?.mode === 'planning'
        ? (view.sections.find((s) => s.rows.some((row) => row.id === focusedId))?.rows ?? null)
        : null,
  };
}

export function placeOptions(context: PlaceContext): PlaceOption[] {
  return [
    { value: TOP, label: 'Top of the backlog' },
    { value: BOTTOM, label: 'Bottom of the backlog' },
    ...(context.focused ? [{ value: BELOW, label: `Below ${context.focused.key}` }] : []),
    ...context.sprints.map((sprint) => ({ value: SPRINT + sprint.id, label: sprint.name })),
  ];
}

/** Below the focused row when there is one, else the top, as the design opens it. */
export const defaultPlace = (context: PlaceContext) => (context.focused ? BELOW : TOP);

type Where = Pick<BacklogAddRequest, 'sprintId' | 'placement'>;

/** The sprint and placement to send (K2) for a choice. */
export function placeRequest(value: string, context: PlaceContext): Where {
  if (value === BOTTOM) return { sprintId: null, placement: { slot: 'last', beforeItemId: null } };
  if (value.startsWith(SPRINT))
    return {
      sprintId: value.slice(SPRINT.length),
      placement: { slot: 'first', beforeItemId: null },
    };
  const { focused } = context;
  if (value === BELOW && focused) {
    const sprintId = focused.sprint?.id ?? null;
    // List: the next row in the shown list, whatever its sprint. Planning: the next row of the focused row's
    // section. When the focused row is the last one shown, `last` with its sprint means directly below that sprint's
    // last row in the server's order (K2), so the new rows land under the focused row, not at the bottom of the backlog.
    const rows =
      context.sectionRows ?? context.order.map((id) => context.rows.find((r) => r.id === id));
    const next = rows[rows.findIndex((row) => row?.id === focused.id) + 1];
    return next
      ? { sprintId, placement: { slot: 'before', beforeItemId: next.id } }
      : { sprintId, placement: { slot: 'last', beforeItemId: null } };
  }
  return { sprintId: null, placement: { slot: 'first', beforeItemId: null } };
}
