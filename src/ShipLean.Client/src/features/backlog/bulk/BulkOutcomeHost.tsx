import { useCallback, useEffect, useState } from 'react';
import { useSelection } from '../selection/useSelection';
import { ChangedRows } from './ChangedRows';
import { UndoToast } from './UndoToast';
import { isUndoPlan, undoOf } from './undoOf';
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

/**
 * What the last bulk run left to decide: the rows a teammate changed, with Include it anyway and Leave it, and Undo.
 * Undo is offered for the latest undoable run only, once, and is itself reported when a row changed since.
 */
export function BulkOutcomeHost({ projectId }: { projectId: string }) {
  void projectId;
  const runner = useBulkRunner();
  const selection = useSelection();
  const { outcome } = runner;
  const [undo, setUndo] = useState<{ outcome: BulkOutcome; message: string } | null>(null);
  const { addMany } = selection;

  // A new outcome offers its Undo; set during render, as React asks, from the last outcome seen.
  const [seen, setSeen] = useState<BulkOutcome | null>(null);
  if (outcome !== seen) {
    setSeen(outcome);
    if (outcome !== null && outcome.plan.undoable && outcome.written.length > 0) {
      setUndo({ outcome, message: outcome.plan.label(outcome.written.length) });
    }
  }

  useEffect(() => {
    if (outcome !== null && outcome.stale.length > 0) {
      addMany(outcome.stale.map((conflict) => conflict.current.id));
    }
  }, [outcome, addMany]);

  const expire = useCallback(() => setUndo(null), []);

  if (outcome === null && undo === null) return null;
  const changed = outcome !== null && outcome.stale.length > 0 ? outcome : null;
  const undoing = changed !== null && isUndoPlan(changed.plan);

  const doUndo = () => {
    if (undo === null) return;
    const source = undo.outcome;
    setUndo(null);
    void runner.run(undoOf(source.viewBefore, source));
  };

  return (
    <>
      {changed ? (
        <ChangedRows
          summary={changed.plan.label(changed.written.length)}
          conflicts={changed.stale}
          canInclude={!undoing}
          onInclude={() => void runner.run(includePlan(changed))}
          onLeave={runner.dismiss}
        />
      ) : null}
      {undo ? (
        <UndoToast
          message={undo.message}
          aboveBar={selection.count > 0}
          onUndo={doUndo}
          onExpire={expire}
        />
      ) : null}
    </>
  );
}
