import { Avatar } from '../../../../components/Avatar';
import { TaskCell } from '../../rows/TaskCell';
import type { TaskboardGridView } from '../../taskboardGridView';
import { taskboardTestIds as ids } from '../../testIds';
import type { PersonLane } from './phonePeopleLanes';

/** A person's lane on one state's page (grouped by People): who, then their tasks here, each naming its Feature and story. */
export function PhonePersonLane({
  view,
  lane,
  stateId,
  meId,
}: {
  view: TaskboardGridView;
  lane: PersonLane;
  stateId: string;
  meId: string | undefined;
}) {
  const { row, rowId, tasks } = lane;
  return (
    <div
      data-testid={ids.lane}
      data-row={rowId}
      data-person
      className="grid min-w-0 grid-cols-[minmax(0,1fr)]"
    >
      <div
        data-testid={ids.rowHeader}
        className="relative z-[1] flex min-h-10 items-center gap-2 rounded-md border border-edge bg-panel px-2"
      >
        <Avatar {...(row.person ?? {})} />
        <b data-testid={ids.rowTitle} className="min-w-0 truncate font-semibold">
          {row.person?.name ?? 'Unassigned'}
        </b>
        {row.person && row.person.id === meId ? (
          <span className="text-[length:var(--text-xs)] text-ink-muted">(you)</span>
        ) : null}
        <span
          data-testid={ids.rowCount}
          className="ml-auto whitespace-nowrap text-[length:var(--text-xs)] text-ink-muted"
        >
          {row.taskCount} {row.taskCount === 1 ? 'task' : 'tasks'}
        </span>
      </div>
      <TaskCell
        lane
        rowId={rowId}
        stateId={stateId}
        cards={tasks}
        view={view}
        showParent
        empty={
          <span
            data-testid={ids.laneNone}
            className="px-1 text-[length:var(--text-sm)] text-ink-muted"
          >
            None
          </span>
        }
      />
    </div>
  );
}
