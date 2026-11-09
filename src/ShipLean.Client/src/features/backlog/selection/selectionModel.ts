import type { BacklogRowDto } from '@shiplean/core/api/views';

// The pure rules of a selection over the rows the screen shows. `order` is every shown row's id in screen order,
// across sprint sections; a selection may hold more rows than one action sends.
export interface SelectionState {
  readonly ids: ReadonlySet<string>;
  /** The row a Shift-click range starts from: the last row toggled on. */
  readonly anchor: string | null;
}

export const emptySelection: SelectionState = { ids: new Set(), anchor: null };

export type SelectionAction =
  | { type: 'toggle'; id: string }
  | { type: 'range'; id: string; order: readonly string[] }
  | { type: 'selectAll'; order: readonly string[] }
  | { type: 'addMany'; ids: readonly string[] }
  | { type: 'removeMany'; ids: readonly string[] }
  | { type: 'clear' }
  | { type: 'prune'; order: readonly string[] };

export function toggle(state: SelectionState, id: string): SelectionState {
  const ids = new Set(state.ids);
  if (ids.delete(id)) return { ids, anchor: state.anchor === id ? null : state.anchor };
  ids.add(id);
  return { ids, anchor: id };
}

/** Adds every row from the anchor to `id`, both ends included, whatever section they are in. With no anchor in view it selects just `id`. */
export function range(state: SelectionState, order: readonly string[], id: string): SelectionState {
  const to = order.indexOf(id);
  const from = state.anchor === null ? -1 : order.indexOf(state.anchor);
  if (to < 0) return state;
  if (from < 0) return toggleOn(state, id);
  const ids = new Set(state.ids);
  for (const between of order.slice(Math.min(from, to), Math.max(from, to) + 1)) ids.add(between);
  return { ids, anchor: state.anchor };
}

function toggleOn(state: SelectionState, id: string): SelectionState {
  return { ids: new Set(state.ids).add(id), anchor: id };
}

export function selectAll(order: readonly string[]): SelectionState {
  return { ids: new Set(order), anchor: order[0] ?? null };
}

/** Adds rows to the selection, keeping the rest (a collapsed section's rows stay selected). */
export function addMany(state: SelectionState, ids: readonly string[]): SelectionState {
  if (ids.length === 0) return state;
  return { ids: new Set([...state.ids, ...ids]), anchor: state.anchor ?? ids[0] };
}

export function removeMany(state: SelectionState, ids: readonly string[]): SelectionState {
  const drop = new Set(ids);
  const kept = [...state.ids].filter((id) => !drop.has(id));
  const anchor = state.anchor !== null && drop.has(state.anchor) ? null : state.anchor;
  return { ids: new Set(kept), anchor };
}

export const clear = (): SelectionState => emptySelection;

/** Drops the rows that left the screen (moved off by a filter, removed, or changed by a teammate). Same state when none left. */
export function prune(state: SelectionState, order: readonly string[]): SelectionState {
  const shown = new Set(order);
  const kept = [...state.ids].filter((id) => shown.has(id));
  const anchor = state.anchor !== null && shown.has(state.anchor) ? state.anchor : null;
  if (kept.length === state.ids.size && anchor === state.anchor) return state;
  return { ids: new Set(kept), anchor };
}

export function selectionReducer(state: SelectionState, action: SelectionAction): SelectionState {
  switch (action.type) {
    case 'toggle':
      return toggle(state, action.id);
    case 'range':
      return range(state, action.order, action.id);
    case 'selectAll':
      return selectAll(action.order);
    case 'addMany':
      return addMany(state, action.ids);
    case 'removeMany':
      return removeMany(state, action.ids);
    case 'clear':
      return state.ids.size === 0 ? state : clear();
    case 'prune':
      return prune(state, action.order);
  }
}

/** The selected rows in the order `rows` gives (rank order), never the order they were clicked in. */
export function selectedRows(
  state: SelectionState,
  rows: readonly BacklogRowDto[],
): BacklogRowDto[] {
  return rows.filter((row) => state.ids.has(row.id));
}
