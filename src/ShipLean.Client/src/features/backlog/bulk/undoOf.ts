import type { BacklogRowDto, BacklogViewDto } from '@shiplean/core/api/views';
import type { WorkItemChanges, WorkItemDto } from '@shiplean/core/api/workItems';
import type { BulkPlacement } from '@shiplean/core/api/backlogWrites';
import { changesOf } from './changesOf';
import type { BulkOutcome, BulkPlan } from './useBulkRun';

/** The rows of each section as the view shows them (List mode is one section), each in rank order. */
function sectionsOf(view: BacklogViewDto): BacklogRowDto[][] {
  return [view.rows, ...view.sections.map((section) => section.rows)].map((rows) =>
    rows
      .map((row, index) => ({ row, index }))
      .sort((a, b) => (a.row.rank ?? Infinity) - (b.row.rank ?? Infinity) || a.index - b.index)
      .map(({ row }) => row),
  );
}

/**
 * The old value of each field `changes` sets, read from the row as it was, or from `held` (the row as a teammate left
 * it, just before Include it anyway) when there is one; null when it set nothing.
 */
function oldValuesOf(
  row: BacklogRowDto,
  changes: WorkItemChanges | null,
  held: WorkItemDto | undefined,
): WorkItemChanges | null {
  if (changes === null) return null;
  const sprint = held ? held.sprint : row.sprint;
  const points = held ? held.points : row.points;
  const state = held ? held.state : row.state;
  const parent = held ? held.parent : row.parent;
  const tags = held ? held.tags : row.tags.map((tag) => tag.name);
  const old: Partial<WorkItemChanges> = {};
  if (changes.sprint) old.sprint = { value: sprint?.id ?? null };
  if (changes.points) old.points = { value: points };
  if (changes.state && state) old.state = { value: state.id };
  if (changes.parent) old.parent = { value: parent?.id ?? null };
  if (changes.tags) old.tags = { value: tags };
  return changesOf(old);
}

const undoPlans = new WeakSet<BulkPlan>();

/** True for a plan `undoOf` made, so its report never offers to overwrite a later change. */
export function isUndoPlan(plan: BulkPlan): boolean {
  return undoPlans.has(plan);
}

/**
 * The plan that puts a run back, built from the rows as they were (`outcome.viewBefore`). Only rows in `written` go
 * back, each with its own old values and the revision the run wrote, so a row a teammate changed since comes back
 * stale and is left alone. `written` may hold rows from several sends of the same plan (the run, then Include it
 * anyway); the placements are built once, from the original order. Section by section, each run of rows that moved
 * together goes back as one block: just above the next row of its section that did not move, or last when there is
 * none. `held` has the rows Include it anyway sent, as they stood just before: their old values come from there.
 */
export function undoOf(
  viewBefore: BacklogViewDto | undefined,
  outcome: BulkOutcome,
  held: ReadonlyMap<string, WorkItemDto> = new Map(),
): BulkPlan {
  const plan = buildUndo(viewBefore, outcome, held);
  undoPlans.add(plan);
  return plan;
}

function buildUndo(
  viewBefore: BacklogViewDto | undefined,
  outcome: BulkOutcome,
  held: ReadonlyMap<string, WorkItemDto>,
): BulkPlan {
  const label = (count: number) => `${count} restored`;
  if (viewBefore === undefined) return { items: [], label, undoable: false, pinRevisions: true };
  const revisions = new Map(outcome.written.map((result) => [result.id, result.revision]));
  const planned = new Map(outcome.plan.items.map((item) => [item.row.id, item]));
  const isMoved = (row: BacklogRowDto) =>
    revisions.has(row.id) && planned.get(row.id)?.placement != null;

  return {
    items: sectionsOf(viewBefore).flatMap((section) => {
      const anchors = new Map<string, string | null>();
      let run: string[] = [];
      section.forEach((row) => {
        if (isMoved(row)) {
          run.push(row.id);
          return;
        }
        run.forEach((id) => anchors.set(id, row.id));
        run = [];
      });
      run.forEach((id) => anchors.set(id, null));

      return section.flatMap((row) => {
        const item = planned.get(row.id);
        const revision = revisions.get(row.id);
        if (item === undefined || revision === undefined) return [];
        const anchor = anchors.get(row.id);
        let placement: BulkPlacement | null = null;
        if (isMoved(row)) {
          placement =
            anchor === null || anchor === undefined
              ? { slot: 'last', beforeItemId: null }
              : { slot: 'before', beforeItemId: anchor };
        }
        return [
          {
            row: { ...row, revision },
            changes: oldValuesOf(row, item.changes, held.get(row.id)),
            placement,
          },
        ];
      });
    }),
    label,
    undoable: false,
    pinRevisions: true,
  };
}
