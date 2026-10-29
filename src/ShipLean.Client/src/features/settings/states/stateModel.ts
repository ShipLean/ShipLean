import type { StateChanges, StateDto, StateUpdate } from '../../../api/projects';
import type { StateCategory, WorkItemTypeId } from '../../../api/refs';
import { TYPE_LABELS } from './categories';

/** One state of one type as the draft would leave it, in order. */
export interface StateRowModel {
  id: string;
  type: WorkItemTypeId;
  name: string;
  category: StateCategory;
  /** Items in the state now (from the server). */
  itemCount: number;
  /** Items that other deleted states move here on Save. */
  incoming: number;
  /** Added in this draft. */
  fresh: boolean;
  /** The name it has on the server; undefined for a fresh state. */
  savedName?: string;
}

const REQUIRED: { category: StateCategory; label: string }[] = [
  { category: 'proposed', label: 'Proposed' },
  { category: 'completed', label: 'Completed' },
];

/** The states of one type as the draft leaves them: saved states with renames applied, added ones at the end, then the staged order. Deleted states are gone. */
export function rowsOf(saved: StateDto[], changes: StateChanges, type: WorkItemTypeId) {
  const removed = new Set(changes.remove.map((r) => r.stateId));
  const incoming = new Map<string, number>();
  const countOf = new Map(saved.map((s) => [s.id, s.itemCount]));
  const finalTarget = (id: string): string => {
    const hop = changes.remove.find((r) => r.stateId === id);
    return hop === undefined ? id : finalTarget(hop.targetStateId);
  };
  for (const r of changes.remove) {
    const to = finalTarget(r.targetStateId);
    incoming.set(to, (incoming.get(to) ?? 0) + (countOf.get(r.stateId) ?? 0));
  }
  const rows: StateRowModel[] = saved
    .filter((s) => s.type === type && !removed.has(s.id))
    .sort((a, b) => a.order - b.order)
    .map((s) => {
      const update = changes.update.find((u) => u.stateId === s.id);
      return {
        id: s.id,
        type,
        name: update?.name ?? s.name,
        category: update?.category ?? s.category,
        itemCount: s.itemCount,
        incoming: incoming.get(s.id) ?? 0,
        fresh: false,
        savedName: s.name,
      };
    });
  for (const a of changes.add.filter((x) => x.type === type)) {
    rows.push({
      id: a.id,
      type,
      name: a.name,
      category: a.category,
      itemCount: 0,
      incoming: incoming.get(a.id) ?? 0,
      fresh: true,
    });
  }
  const wanted = changes.reorder.find((r) => r.type === type)?.stateIds;
  if (wanted === undefined) return rows;
  const rank = (id: string) => {
    const at = wanted.indexOf(id);
    return at === -1 ? wanted.length : at;
  };
  return rows
    .map((row, index) => ({ row, index }))
    .sort((a, b) => rank(a.row.id) - rank(b.row.id) || a.index - b.index)
    .map((x) => x.row);
}

export const itemsIn = (row: StateRowModel) => row.itemCount + row.incoming;

/** Why a state can't be deleted before Save, or undefined. The server refuses a type left with no Proposed or Completed state. */
export function deleteBlock(rows: StateRowModel[], id: string): string | undefined {
  const row = rows.find((r) => r.id === id);
  if (row === undefined) return undefined;
  if (rows.length <= 1) return 'A work item type needs at least one state.';
  for (const need of REQUIRED) {
    if (
      row.category === need.category &&
      rows.filter((r) => r.category === need.category).length === 1
    ) {
      return `This is the last ${need.label} state. Add or recategorise another first.`;
    }
  }
  return undefined;
}

/** Problems with a type's list the server would refuse: a blank or repeated name, or no Proposed or Completed state. */
export function typeProblems(rows: StateRowModel[]) {
  const rowErrors = new Map<string, string>();
  for (const row of rows) {
    const name = row.name.trim();
    if (name === '') {
      rowErrors.set(row.id, 'Give the state a name.');
    } else if (rows.some((o) => o !== row && o.name.trim().toLowerCase() === name.toLowerCase())) {
      rowErrors.set(row.id, `Another state is already called ${name}.`);
    }
  }
  const missing = REQUIRED.find((r) => !rows.some((x) => x.category === r.category));
  const typeError =
    missing === undefined
      ? undefined
      : `This type needs a ${missing.label} state${missing.category === 'proposed' ? ': new items start in the first one' : ': velocity and burndown count it'}.`;
  return { rowErrors, typeError };
}

const sameIds = (a: string[], b: string[]) =>
  a.length === b.length && a.every((id, i) => id === b[i]);

/** Adds a state to its type, before the Removed states (which stay last). Sends an explicit order when that differs from the server's (new states last). */
export function addState(
  changes: StateChanges,
  saved: StateDto[],
  add: { id: string; type: WorkItemTypeId; name: string; category: StateCategory },
): StateChanges {
  const added = { ...changes, add: [...changes.add, add] };
  const ids = rowsOf(saved, added, add.type)
    .map((r) => r.id)
    .filter((id) => id !== add.id);
  const firstRemoved = rowsOf(saved, added, add.type).findIndex(
    (r) => r.id !== add.id && r.category === 'removed',
  );
  ids.splice(firstRemoved === -1 ? ids.length : firstRemoved, 0, add.id);
  return setOrder(added, saved, add.type, ids);
}

/** Renames and/or recategorises a state; an edit that puts it back as saved drops the change. */
export function editState(
  changes: StateChanges,
  saved: StateDto[],
  id: string,
  edit: { name?: string; category?: StateCategory },
): StateChanges {
  if (changes.add.some((a) => a.id === id)) {
    return { ...changes, add: changes.add.map((a) => (a.id === id ? { ...a, ...edit } : a)) };
  }
  const was = saved.find((s) => s.id === id);
  if (was === undefined) return changes;
  const now: StateUpdate = changes.update.find((u) => u.stateId === id) ?? {
    stateId: id,
    name: was.name,
    category: was.category,
  };
  const next: StateUpdate = { ...now, ...edit };
  const others = changes.update.filter((u) => u.stateId !== id);
  const unchanged = next.name === was.name && next.category === was.category;
  return { ...changes, update: unchanged ? others : [...others, next] };
}

/** Sets the full order of one type; the default order (saved, then added) drops the change. */
export function setOrder(
  changes: StateChanges,
  saved: StateDto[],
  type: WorkItemTypeId,
  ids: string[],
): StateChanges {
  const others = changes.reorder.filter((r) => r.type !== type);
  const removed = new Set(changes.remove.map((r) => r.stateId));
  const natural = [
    ...saved
      .filter((s) => s.type === type && !removed.has(s.id))
      .sort((a, b) => a.order - b.order)
      .map((s) => s.id),
    ...changes.add.filter((a) => a.type === type).map((a) => a.id),
  ];
  return {
    ...changes,
    reorder: sameIds(ids, natural) ? others : [...others, { type, stateIds: ids }],
  };
}

/** Moves a state one place up (-1) or down (1) in its type. */
export function moveState(
  changes: StateChanges,
  saved: StateDto[],
  type: WorkItemTypeId,
  id: string,
  by: -1 | 1,
): StateChanges {
  const ids = rowsOf(saved, changes, type).map((r) => r.id);
  const from = ids.indexOf(id);
  const to = from + by;
  if (from === -1 || to < 0 || to >= ids.length) return changes;
  [ids[from], ids[to]] = [ids[to] ?? id, ids[from] ?? id];
  return setOrder(changes, saved, type, ids);
}

/** Deletes a state; its items (and any other deleted state's items headed for it) go to `targetId`. A state added in this draft just disappears. */
export function removeState(changes: StateChanges, id: string, targetId: string): StateChanges {
  const dropFrom = (ids: string[]) => ids.filter((x) => x !== id);
  const reorder = changes.reorder.map((r) => ({ ...r, stateIds: dropFrom(r.stateIds) }));
  if (changes.add.some((a) => a.id === id)) {
    const without = { ...changes, add: changes.add.filter((a) => a.id !== id), reorder };
    return {
      ...without,
      remove: without.remove.map((r) =>
        r.targetStateId === id ? { ...r, targetStateId: targetId } : r,
      ),
    };
  }
  return {
    add: changes.add,
    update: changes.update.filter((u) => u.stateId !== id),
    reorder,
    remove: [
      ...changes.remove.map((r) =>
        r.targetStateId === id ? { ...r, targetStateId: targetId } : r,
      ),
      { stateId: id, targetStateId: targetId },
    ],
  };
}

/** Where a deleted state's items go unless the user picks: another state of the same category, then the first Proposed, then any. */
export function defaultTarget(rows: StateRowModel[], id: string): string | undefined {
  const row = rows.find((r) => r.id === id);
  if (row === undefined) return undefined;
  const others = rows.filter((r) => r.id !== id);
  return (
    others.find((r) => r.category === row.category) ??
    others.find((r) => r.category === 'proposed') ??
    others[0]
  )?.id;
}

/**
 * A teammate saved states while this draft had state edits. `latest` is their document, `base` the one this draft was
 * made from. Drops what names a state that is gone, points a delete whose target is gone at another state, and keeps
 * only the fields the user changed of a rename or recategorise, so a teammate's change to the other field survives.
 */
export function rebaseStates(
  latest: StateDto[],
  base: StateDto[],
  changes: StateChanges,
): StateChanges {
  const live = new Map(latest.map((s) => [s.id, s]));
  const baseOf = new Map(base.map((s) => [s.id, s]));
  const fresh = new Map(changes.add.map((a) => [a.id, a]));
  const exists = (id: string) => live.has(id) || fresh.has(id);

  const update = changes.update.flatMap((u) => {
    const now = live.get(u.stateId);
    const was = baseOf.get(u.stateId);
    if (now === undefined || was === undefined) return [];
    const name = u.name === was.name ? now.name : u.name;
    const category = u.category === was.category ? now.category : u.category;
    return name === now.name && category === now.category
      ? []
      : [{ stateId: u.stateId, name, category }];
  });

  const removing = new Set(changes.remove.filter((r) => live.has(r.stateId)).map((r) => r.stateId));
  const remove = changes.remove.flatMap((r) => {
    const from = live.get(r.stateId);
    if (from === undefined) return [];
    if (exists(r.targetStateId) && !removing.has(r.targetStateId)) return [r];
    const pool = [
      ...latest.filter((s) => s.type === from.type),
      ...changes.add.filter((a) => a.type === from.type),
    ].filter((s) => s.id !== r.stateId && !removing.has(s.id));
    const target =
      pool.find((s) => s.category === from.category) ??
      pool.find((s) => s.category === 'proposed') ??
      pool[0];
    return target === undefined ? [] : [{ stateId: r.stateId, targetStateId: target.id }];
  });

  const reorder = changes.reorder.flatMap((o) => {
    const stateIds = o.stateIds.filter(exists);
    return stateIds.length === 0 ? [] : [{ ...o, stateIds }];
  });
  return { add: changes.add, update, reorder, remove };
}

/** "Bug: deleted Active, 3 items move to New" for the change list. */
export function removeLabel(saved: StateDto[], changes: StateChanges, stateId: string): string {
  const gone = saved.find((s) => s.id === stateId);
  if (gone === undefined) return 'Deleted a state';
  const targetId = changes.remove.find((r) => r.stateId === stateId)?.targetStateId;
  const target =
    saved.find((s) => s.id === targetId)?.name ?? changes.add.find((a) => a.id === targetId)?.name;
  const head = `${TYPE_LABELS[gone.type]}: deleted ${gone.name}`;
  if (gone.itemCount === 0 || target === undefined) return head;
  return `${head}, ${gone.itemCount} ${gone.itemCount === 1 ? 'item moves' : 'items move'} to ${target}`;
}
