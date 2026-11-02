import { useCallback, useEffect, useRef, useState } from 'react';
import { useSelection } from '../selection/useSelection';
import { ChangedRows } from './ChangedRows';
import { UndoToast } from './UndoToast';
import { isUndoPlan, mergeUndo, undoOf } from './undoOf';
import { useBulkRunner, type BulkOutcome, type BulkPlan } from './useBulkRun';

/** The run's rows a teammate changed, sent again against the revision they have now. */
function includePlan(outcome: BulkOutcome): BulkPlan {
  const revisions = new Map(
    outcome.stale.map((conflict) => [conflict.current.id, conflict.current.revision]),
  );
  return {
    items: outcome.plan.items
      .filter(({ row }) => revisions.has(row.id))
      .map((item) => ({
        ...item,
        row: { ...item.row, revision: revisions.get(item.row.id) ?? item.row.revision },
      })),
    label: outcome.plan.label,
    undoable: false,
    pinRevisions: true,
  };
}

/** The run Undo puts back: the first run, then the rows Include it anyway wrote after it. */
interface PendingUndo {
  runs: BulkOutcome[];
}

const writtenIn = (runs: BulkOutcome[]) => runs.reduce((sum, run) => sum + run.written.length, 0);

/**
 * What the last bulk run left to decide: the rows a teammate changed, with Include it anyway and Leave it, and Undo.
 * Undo is offered for the latest undoable run only, once, and is itself reported when a row changed since. Rows
 * written by Include it anyway join the pending Undo. An Undo whose request failed is offered again.
 */
export function BulkOutcomeHost({ projectId }: { projectId: string }) {
  void projectId;
  const runner = useBulkRunner();
  const selection = useSelection();
  const { outcome } = runner;
  const [undo, setUndo] = useState<PendingUndo | null>(null);

  // A new outcome offers its Undo; set during render, as React asks, from the last outcome seen.
  const [seen, setSeen] = useState<BulkOutcome | null>(null);
  if (outcome !== seen) {
    setSeen(outcome);
    if (outcome !== null && outcome.plan.undoable && outcome.written.length > 0) {
      setUndo({ runs: [outcome] });
    }
  }

  // The selection changes identity on every update, so the effect reads it through a ref and runs once per outcome.
  const latest = useRef(selection);
  const reselected = useRef<BulkOutcome | null>(null);
  useEffect(() => {
    latest.current = selection;
  });
  useEffect(() => {
    if (outcome === null || reselected.current === outcome) return;
    reselected.current = outcome;
    const missing = outcome.stale
      .map((conflict) => conflict.current.id)
      .filter((id) => !latest.current.has(id));
    if (missing.length > 0) latest.current.addMany(missing);
  }, [outcome]);

  const expire = useCallback(() => setUndo(null), []);

  if (outcome === null && undo === null) return null;
  const changed = outcome !== null && outcome.stale.length > 0 ? outcome : null;
  const undoing = changed !== null && isUndoPlan(changed.plan);

  const doUndo = async () => {
    if (undo === null) return;
    const source = undo;
    setUndo(null);
    const plans = source.runs.map((run) => undoOf(run.viewBefore, run));
    const result = await runner.run(mergeUndo(plans));
    if (result.failed) setUndo((current) => current ?? source);
  };

  const include = async (stale: BulkOutcome) => {
    const result = await runner.run(includePlan(stale));
    if (result.failed || result.written.length === 0) return;
    setUndo((current) => (current === null ? null : { runs: [...current.runs, result] }));
  };

  return (
    <>
      {changed ? (
        <ChangedRows
          summary={changed.plan.label(changed.written.length)}
          conflicts={changed.stale}
          canInclude={!undoing}
          onInclude={() => void include(changed)}
          onLeave={runner.dismiss}
        />
      ) : null}
      {undo ? (
        <UndoToast
          message={undo.runs[0].plan.label(writtenIn(undo.runs))}
          aboveBar={selection.count > 0}
          onUndo={() => void doUndo()}
          onExpire={expire}
        />
      ) : null}
    </>
  );
}
