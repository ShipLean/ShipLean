import { createContext, useContext, useState } from 'react';
import { useQueryClient, type QueryKey } from '@tanstack/react-query';
import { api } from '../../../api/client';
import {
  BULK_MAX_ROWS,
  type BulkPlacement,
  type BulkWriteRequest,
  type BulkWriteResult,
} from '../../../api/backlogWrites';
import type { ConflictDto, WorkItemChanges, WriteResult } from '../../../api/workItems';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import { showToast } from '../../../components/toastStore';
import { backlogRootKey } from '../../../queries/backlogKeys';

/** What a bulk action does to each row. `label(count)` is the toast, e.g. "3 moved to Sprint 25". */
export interface BulkPlan {
  items: { row: BacklogRowDto; changes: WorkItemChanges | null; placement: BulkPlacement | null }[];
  label: (count: number) => string;
  undoable: boolean;
}

/** viewBefore is the backlog as it was just before the send, for Undo. */
export interface BulkOutcome {
  plan: BulkPlan;
  written: WriteResult[];
  stale: ConflictDto[];
  viewBefore: BacklogViewDto | undefined;
  /** True on a refused or failed run, absent on a normal one: nothing was written, keep the selection. */
  failed?: true;
}

export interface BulkRunner {
  run: (plan: BulkPlan) => Promise<BulkOutcome>;
  /** The last run's outcome, until dismissed. */
  outcome: BulkOutcome | null;
  dismiss: () => void;
}

export const BulkRunContext = createContext<BulkRunner | null>(null);

/** The runner the screen's BulkRunProvider shares between the bar, the drag and the keys. */
export function useBulkRunner(): BulkRunner {
  const runner = useContext(BulkRunContext);
  if (runner === null) throw new Error('useBulkRunner needs a BulkRunProvider');
  return runner;
}

function rowsOf(view: BacklogViewDto | undefined): BacklogRowDto[] {
  return view === undefined ? [] : [...view.rows, ...view.sections.flatMap((s) => s.rows)];
}

/**
 * Sends a plan as one POST (K1), whatever the plan holds up to BULK_MAX_ROWS. Each row goes with the revision the
 * backlog holds when it is sent, not the one the plan was made from. A stale row comes back in `stale`; the backlog
 * is read again whatever the answer.
 */
export function useBulkRun(projectId: string, queryKey: QueryKey): BulkRunner {
  const queryClient = useQueryClient();
  const [outcome, setOutcome] = useState<BulkOutcome | null>(null);

  async function run(plan: BulkPlan): Promise<BulkOutcome> {
    const viewBefore = queryClient.getQueryData<BacklogViewDto>(queryKey);
    if (plan.items.length === 0) return { plan, written: [], stale: [], viewBefore };
    if (plan.items.length > BULK_MAX_ROWS) {
      showToast(`Select ${BULK_MAX_ROWS} rows or fewer`, { tone: 'danger' });
      return { plan, written: [], stale: [], viewBefore, failed: true };
    }
    const current = new Map(rowsOf(viewBefore).map((row) => [row.id, row.revision]));
    const body: BulkWriteRequest = {
      items: plan.items.map(({ row, changes, placement }) => ({
        id: row.id,
        expectedRevision: current.get(row.id) ?? row.revision,
        changes,
        placement,
      })),
    };
    let result: BulkWriteResult;
    try {
      result = await api.post<BulkWriteResult>(
        `/api/projects/${encodeURIComponent(projectId)}/backlog/bulk`,
        body,
      );
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not apply the change', {
        tone: 'danger',
      });
      await queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) });
      return { plan, written: [], stale: [], viewBefore, failed: true };
    }
    const done: BulkOutcome = { plan, written: result.written, stale: result.stale, viewBefore };
    setOutcome(done);
    if (done.written.length > 0) showToast(plan.label(done.written.length));
    await queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) });
    return done;
  }

  return { run, outcome, dismiss: () => setOutcome(null) };
}
