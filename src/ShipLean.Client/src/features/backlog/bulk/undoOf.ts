import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import type { WorkItemChanges } from '../../../api/workItems';
import type { BulkPlacement } from '../../../api/backlogWrites';
import { changesOf } from './changesOf';
import type { BulkOutcome, BulkPlan } from './useBulkRun';

function rowsOf(view: BacklogViewDto): BacklogRowDto[] {
  return [...view.rows, ...view.sections.flatMap((section) => section.rows)];
}

/** The old value of each field `changes` sets, read from the row as it was; null when it set nothing. */
function oldValuesOf(row: BacklogRowDto, changes: WorkItemChanges | null): WorkItemChanges | null {
  if (changes === null) return null;
  const old: Partial<WorkItemChanges> = {};
  if (changes.sprint) old.sprint = { value: row.sprint?.id ?? null };
  if (changes.points) old.points = { value: row.points };
  if (changes.state && row.state) old.state = { value: row.state.id };
  if (changes.parent) old.parent = { value: row.parent?.id ?? null };
  if (changes.tags) old.tags = { value: row.tags.map((tag) => tag.name) };
  return changesOf(old);
}

const undoPlans = new WeakSet<BulkPlan>();

/** True for a plan `undoOf` made, so its report never offers to overwrite a later change. */
export function isUndoPlan(plan: BulkPlan): boolean {
  return undoPlans.has(plan);
}

/** One Undo for several runs' undo plans (the run, then Include it anyway's rows), sent as a single request. */
export function mergeUndo(plans: BulkPlan[]): BulkPlan {
  const merged: BulkPlan = {
    items: plans.flatMap((plan) => plan.items),
    label: plans[0]?.label ?? ((count) => `${count} restored`),
    undoable: false,
    pinRevisions: true,
  };
  undoPlans.add(merged);
  return merged;
}

/**
 * The plan that puts a run back, built from the rows as they were (`outcome.viewBefore`). Only rows in `written` go
 * back, each with its own old values and the revision the run wrote, so a row a teammate changed since comes back
 * stale and is left alone. Each run of rows that moved together goes back as one block: just above the next row in
 * the old order that did not move, or last when there is none.
 */
export function undoOf(viewBefore: BacklogViewDto | undefined, outcome: BulkOutcome): BulkPlan {
  const plan = buildUndo(viewBefore, outcome);
  undoPlans.add(plan);
  return plan;
}

function buildUndo(viewBefore: BacklogViewDto | undefined, outcome: BulkOutcome): BulkPlan {
  const label = (count: number) => `${count} restored`;
  if (viewBefore === undefined) return { items: [], label, undoable: false, pinRevisions: true };
  const revisions = new Map(outcome.written.map((result) => [result.id, result.revision]));
  const planned = new Map(outcome.plan.items.map((item) => [item.row.id, item]));
  const oldOrder = rowsOf(viewBefore)
    .map((row, index) => ({ row, index }))
    .sort((a, b) => (a.row.rank ?? Infinity) - (b.row.rank ?? Infinity) || a.index - b.index)
    .map(({ row }) => row);
  const moved = new Set(
    oldOrder
      .filter((row) => revisions.has(row.id) && planned.get(row.id)?.placement)
      .map((r) => r.id),
  );

  const anchors = new Map<string, string | null>();
  let run: string[] = [];
  oldOrder.forEach((row) => {
    if (moved.has(row.id)) {
      run.push(row.id);
      return;
    }
    run.forEach((id) => anchors.set(id, row.id));
    run = [];
  });
  run.forEach((id) => anchors.set(id, null));

  const items = oldOrder.flatMap((row) => {
    const item = planned.get(row.id);
    const revision = revisions.get(row.id);
    if (item === undefined || revision === undefined) return [];
    const anchor = anchors.get(row.id);
    let placement: BulkPlacement | null = null;
    if (moved.has(row.id)) {
      placement =
        anchor === null || anchor === undefined
          ? { slot: 'last', beforeItemId: null }
          : { slot: 'before', beforeItemId: anchor };
    }
    return [{ row: { ...row, revision }, changes: oldValuesOf(row, item.changes), placement }];
  });
  return { items, label, undoable: false, pinRevisions: true };
}
