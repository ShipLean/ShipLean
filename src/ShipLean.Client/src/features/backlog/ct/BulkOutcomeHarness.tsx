import { ToastHost } from '../../../components/ToastHost';
import { backlogKey, useBacklog } from '../backlogApi';
import { BulkOutcomeHost } from '../bulk/BulkOutcomeHost';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { changesOf } from '../bulk/changesOf';
import { moveToEdgePlan, moveToSprintPlan } from '../bulk/movePlan';
import { useBulkRunner, type BulkPlan } from '../bulk/useBulkRun';
import { SelectionProvider } from '../selection/SelectionProvider';
import { useSelection } from '../selection/useSelection';

const SPRINT = { id: 'sp25', name: 'Sprint 25', status: 'future' } as const;

function Panel() {
  const backlog = useBacklog({
    projectId: 'p1',
    level: 'stories',
    mode: 'list',
    filter: '',
    includeCompleted: false,
  });
  const runner = useBulkRunner();
  const rows = backlog.data?.rows ?? [];
  const selection = useSelection(rows.map((row) => row.id));
  // Like the bar: clear before the send, and bring the rows back when nothing was written.
  const run = (plan: BulkPlan) => {
    const ids = [...selection.ids];
    selection.clear();
    void runner.run(plan).then((outcome) => {
      if (outcome.failed) selection.addMany(ids);
    });
  };
  return (
    <div>
      <span data-testid="selected-keys">
        {rows
          .filter((row) => selection.has(row.id))
          .map((row) => row.key)
          .join(',')}
      </span>
      <button type="button" data-testid="clear-selection" onClick={() => selection.clear()}>
        clear
      </button>
      <button type="button" data-testid="select-all" onClick={() => selection.selectAll()}>
        all
      </button>
      <button
        type="button"
        data-testid="run-sprint"
        onClick={() => run(moveToSprintPlan(selection.selectedRows(rows), SPRINT))}
      >
        sprint
      </button>
      <button
        type="button"
        data-testid="run-top"
        onClick={() => run(moveToEdgePlan(selection.selectedRows(rows), 'first'))}
      >
        top
      </button>
      <button
        type="button"
        data-testid="run-points"
        onClick={() =>
          run({
            items: selection.selectedRows(rows).map((row) => ({
              row,
              changes: changesOf({ points: { value: 13 } }),
              placement: null,
            })),
            label: (count) => `${count} estimated`,
            undoable: false,
          })
        }
      >
        points
      </button>
    </div>
  );
}

// CT-only: runs bulk plans over the selected rows of the list and mounts the outcome host.
export function BulkOutcomeHarness() {
  return (
    <BulkRunProvider projectId="p1" queryKey={backlogKey('p1', 'stories', 'list', '', false)}>
      <SelectionProvider>
        <ToastHost />
        <Panel />
        <BulkOutcomeHost projectId="p1" />
      </SelectionProvider>
    </BulkRunProvider>
  );
}
