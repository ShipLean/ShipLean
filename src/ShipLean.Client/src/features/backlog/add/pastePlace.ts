import type { BacklogAddRequest } from '../../../api/backlogWrites';
import type { SprintRef } from '../../../api/refs';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';

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
  /** The shown order of row ids, to find the row after the focused one. */
  order: readonly string[];
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
  return {
    rows,
    sprints: [...seen.values()],
    focused: rows.find((row) => row.id === focusedId) ?? null,
    order,
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
    const nextId = context.order[context.order.indexOf(focused.id) + 1];
    const next = context.rows.find((row) => row.id === nextId);
    // A next row in another sprint (or none) is outside the focused row's scope: land last in that scope.
    return next !== undefined && (next.sprint?.id ?? null) === sprintId
      ? { sprintId, placement: { slot: 'before', beforeItemId: next.id } }
      : { sprintId, placement: { slot: 'last', beforeItemId: null } };
  }
  return { sprintId: null, placement: { slot: 'first', beforeItemId: null } };
}
