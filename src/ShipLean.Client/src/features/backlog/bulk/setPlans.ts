import type { StateDto } from '../../../api/projects';
import type { WorkItemTypeId } from '../../../api/refs';
import type { BacklogRowDto } from '../../../api/views';
import { changesOf } from './changesOf';
import type { BulkPlan } from './useBulkRun';

/** A plan, and what it leaves out: `skipped` is the sentence for rows the action cannot reach, else null. */
export interface SetPlan {
  plan: BulkPlan;
  skipped: string | null;
}

const many = (n: number) => `${n} item${n === 1 ? '' : 's'}`;

function plural(n: number, name: string): string {
  const lower = name.toLowerCase();
  if (n === 1) return `1 ${lower}`;
  return `${n} ${lower.endsWith('y') ? `${lower.slice(0, -1)}ies` : `${lower}s`}`;
}

/** "2 bugs have no In review" for rows whose type has no state of that name. */
function skippedText(missing: readonly BacklogRowDto[], stateName: string): string | null {
  if (missing.length === 0) return null;
  const byType = new Map<string, number>();
  for (const row of missing) {
    const name = row.type?.name ?? 'item';
    byType.set(name, (byType.get(name) ?? 0) + 1);
  }
  return [...byType]
    .map(([name, n]) => `${plural(n, name)} ${n === 1 ? 'has' : 'have'} no ${stateName}`)
    .join(', ');
}

const withSkipped = (text: string, skipped: string | null) =>
  skipped === null ? text : `${text}; ${skipped}`;

/** Sets the estimate on every row; null clears it. */
export function setPointsPlan(rows: readonly BacklogRowDto[], points: number | null): SetPlan {
  return {
    plan: {
      items: rows.map((row) => ({
        row,
        changes: changesOf({ points: { value: points } }),
        placement: null,
      })),
      label: (count) =>
        points === null ? `Estimate cleared on ${many(count)}` : `${points} pts on ${many(count)}`,
      undoable: true,
    },
    skipped: null,
  };
}

/** The state names any of these rows' types has, in workflow order, without Removed. */
export function stateNamesFor(
  rows: readonly BacklogRowDto[],
  states: readonly StateDto[],
): string[] {
  const types = new Set<string>(rows.map((row) => row.type?.id ?? ''));
  const names: string[] = [];
  for (const state of [...states].sort((a, b) => a.order - b.order)) {
    if (!types.has(state.type) || state.category === 'removed') continue;
    if (!names.includes(state.name)) names.push(state.name);
  }
  return names;
}

function planWithState(
  rows: readonly BacklogRowDto[],
  pick: (type: WorkItemTypeId) => StateDto | undefined,
  stateName: string,
  label: (count: number) => string,
): SetPlan {
  const items: BulkPlan['items'] = [];
  const missing: BacklogRowDto[] = [];
  for (const row of rows) {
    const state = row.type === null ? undefined : pick(row.type.id);
    if (state === undefined) missing.push(row);
    else items.push({ row, changes: changesOf({ state: { value: state.id } }), placement: null });
  }
  const skipped = skippedText(missing, stateName);
  return {
    plan: { items, label: (count) => withSkipped(label(count), skipped), undoable: true },
    skipped,
  };
}

/** Sets the state of that name on each row's own type; a row whose type lacks it is left out and counted. */
export function setStatePlan(
  rows: readonly BacklogRowDto[],
  states: readonly StateDto[],
  stateName: string,
): SetPlan {
  return planWithState(
    rows,
    (type) => states.find((s) => s.type === type && s.name === stateName),
    stateName,
    (count) => `${stateName} on ${many(count)}`,
  );
}

/** Moves each row to its own type's Removed state. */
export function removePlan(rows: readonly BacklogRowDto[], states: readonly StateDto[]): SetPlan {
  return planWithState(
    rows,
    (type) => states.find((s) => s.type === type && s.category === 'removed'),
    'Removed state',
    (count) => `${many(count)} set to Removed`,
  );
}

/** Adds the tag to each row's own tags; a row that has it is written unchanged. */
export function setTagPlan(rows: readonly BacklogRowDto[], tag: string): SetPlan {
  return {
    plan: {
      items: rows.map((row) => ({
        row,
        changes: changesOf({
          tags: { value: [...new Set([...row.tags.map((t) => t.name), tag])] },
        }),
        placement: null,
      })),
      label: (count) => `Tag ${tag} on ${many(count)}`,
      undoable: true,
    },
    skipped: null,
  };
}

/** Sets the parent on every row; null clears it. `title` is for the toast. */
export function setParentPlan(
  rows: readonly BacklogRowDto[],
  parent: { id: string; title: string } | null,
): SetPlan {
  return {
    plan: {
      items: rows.map((row) => ({
        row,
        changes: changesOf({ parent: { value: parent?.id ?? null } }),
        placement: null,
      })),
      label: (count) =>
        parent === null
          ? `Parent cleared on ${many(count)}`
          : `Parent ${parent.title} on ${many(count)}`,
      undoable: true,
    },
    skipped: null,
  };
}
