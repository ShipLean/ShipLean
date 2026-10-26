import type { StateRef, WorkItemTypeId } from '../../../../api/refs';
import type { BoardColumnJson } from '../../../../api/persisted';
import type { BoardTypeStatesDto } from '../../../../api/boards';

export const typeNames: Record<WorkItemTypeId, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'Story',
  bug: 'Bug',
  task: 'Task',
};

export interface UnmappedState {
  type: WorkItemTypeId;
  state: StateRef;
}

/** What the one-click fix does: point a column at the state for its type, or add a column for it. */
export type Fix =
  | { kind: 'remap'; column: BoardColumnJson; unmapped: UnmappedState }
  | { kind: 'add'; unmapped: UnmappedState };

export const liveStates = (states: BoardTypeStatesDto[], type: WorkItemTypeId): StateRef[] =>
  states.find((entry) => entry.type === type)?.states.filter((s) => s.category !== 'removed') ?? [];

export const stateOf = (column: BoardColumnJson, type: WorkItemTypeId): string | undefined =>
  column.stateMappings.find((mapping) => mapping.type === type)?.stateId;

export function unmappedStates(
  columns: BoardColumnJson[],
  states: BoardTypeStatesDto[],
): UnmappedState[] {
  const mapped = new Set(columns.flatMap((c) => c.stateMappings.map((m) => m.stateId)));
  return states.flatMap(({ type, states: own }) =>
    own
      .filter((s) => s.category !== 'removed' && !mapped.has(s.id))
      .map((state) => ({ type, state })),
  );
}

export interface DeadMapping {
  column: BoardColumnJson;
  type: WorkItemTypeId;
}

/** Columns that map a type to a state that is no longer live (archived, or moved to Removed), or to none at all. */
export function deadMappings(
  columns: BoardColumnJson[],
  states: BoardTypeStatesDto[],
): DeadMapping[] {
  return columns.flatMap((column) =>
    states
      .filter(({ type }) => {
        const live = liveStates(states, type);
        return live.length > 0 && !live.some((s) => s.id === stateOf(column, type));
      })
      .map(({ type }) => ({ column, type })),
  );
}

/**
 * Remaps the last column whose state for this type has the same category and is also on another column, so the fix
 * opens no new gap; a state with no such column gets a column of its own.
 */
export function fixFor(
  columns: BoardColumnJson[],
  states: BoardTypeStatesDto[],
  unmapped: UnmappedState,
): Fix {
  const own = liveStates(states, unmapped.type);
  const categoryOf = (id: string | undefined) => own.find((s) => s.id === id)?.category;
  const count = (id: string | undefined) =>
    columns.filter((c) => stateOf(c, unmapped.type) === id).length;
  const candidates = columns.filter((c) => {
    const id = stateOf(c, unmapped.type);
    return categoryOf(id) === unmapped.state.category && count(id) > 1;
  });
  const column = candidates[candidates.length - 1];
  return column === undefined ? { kind: 'add', unmapped } : { kind: 'remap', column, unmapped };
}

export function setState(
  column: BoardColumnJson,
  type: WorkItemTypeId,
  stateId: string,
): BoardColumnJson {
  const others = column.stateMappings.filter((m) => m.type !== type);
  return { ...column, stateMappings: [...others, { type, stateId }] };
}

export function replaceColumn(
  columns: BoardColumnJson[],
  id: string,
  next: BoardColumnJson,
): BoardColumnJson[] {
  return columns.map((c) => (c.id === id ? next : c));
}

/** Moves a middle column one place or to a place; the first and last columns stay where they are. */
export function moveColumn(
  columns: BoardColumnJson[],
  from: number,
  to: number,
): BoardColumnJson[] {
  const last = columns.length - 1;
  if (from < 1 || from > last - 1 || to < 1 || to > last - 1 || from === to) return columns;
  const next = [...columns];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

/** A column goes in before the last one. */
export function insertBeforeLast(
  columns: BoardColumnJson[],
  column: BoardColumnJson,
): BoardColumnJson[] {
  if (columns.length === 0) return [column];
  return [...columns.slice(0, -1), column, columns[columns.length - 1]];
}

export const newColumnId = () => `col-${crypto.randomUUID().slice(0, 8)}`;

/** The state a new column starts on for one type: the first In progress state, else the first live one. */
export function startingState(
  states: BoardTypeStatesDto[],
  type: WorkItemTypeId,
): string | undefined {
  const own = liveStates(states, type);
  return (own.find((s) => s.category === 'inProgress') ?? own[0])?.id;
}

/** One mapping per type that has a live state to map; a type with none is left out. */
const mappingsWith = (
  states: BoardTypeStatesDto[],
  stateFor: (type: WorkItemTypeId) => string | undefined,
): BoardColumnJson['stateMappings'] =>
  states.flatMap(({ type }) => {
    const stateId = stateFor(type);
    return stateId === undefined ? [] : [{ type, stateId }];
  });

export function blankColumn(states: BoardTypeStatesDto[]): BoardColumnJson {
  return {
    id: newColumnId(),
    name: 'New column',
    wipLimit: null,
    split: false,
    definitionOfDone: '',
    stateMappings: mappingsWith(states, (type) => startingState(states, type)),
  };
}

/** A column named after the state: that state for its type, the same category (else the starting state) for the rest. */
export function columnForState(
  states: BoardTypeStatesDto[],
  unmapped: UnmappedState,
): BoardColumnJson {
  return {
    ...blankColumn(states),
    name: unmapped.state.name,
    stateMappings: mappingsWith(states, (type) =>
      type === unmapped.type
        ? unmapped.state.id
        : (liveStates(states, type).find((s) => s.category === unmapped.state.category)?.id ??
          startingState(states, type)),
    ),
  };
}

export function parseWipLimit(text: string): number | null {
  const value = Number.parseInt(text, 10);
  return Number.isNaN(value) ? null : Math.max(1, value);
}
