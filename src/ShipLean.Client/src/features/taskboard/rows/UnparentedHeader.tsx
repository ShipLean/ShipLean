import { TypeIcon } from '../../../components/TypeIcon';
import { taskboardTestIds as ids } from '../testIds';
import { RowToggle } from './RowToggle';

/** The header of the Unparented row: tasks with no story or bug. */
export function UnparentedHeader({
  count,
  collapsed,
  onToggle,
  compact = false,
}: {
  count: number;
  collapsed: boolean;
  onToggle: () => void;
  /** The phone lane's dashed header: ▼, title and "n tasks · no points" on one line. */
  compact?: boolean;
}) {
  const tasks = `${count} ${count === 1 ? 'task' : 'tasks'}`;
  if (compact) {
    return (
      <div
        data-testid={ids.rowHeader}
        className="flex min-h-10 items-center gap-2 rounded-control border border-dashed border-edge bg-canvas py-0 pl-0.5 pr-1"
      >
        <RowToggle collapsed={collapsed} onToggle={onToggle} label="unparented tasks" />
        <b data-testid={ids.rowTitle} className="min-w-0 flex-1 truncate font-semibold">
          Unparented
        </b>
        <span
          data-testid={ids.rowCount}
          className="whitespace-nowrap pr-1.5 text-[length:var(--text-xs)] text-ink-muted"
        >
          {tasks} · no points
        </span>
      </div>
    );
  }
  return (
    <div data-testid={ids.rowHeader} className="grid gap-1 px-2 py-2">
      <div className="flex items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <RowToggle collapsed={collapsed} onToggle={onToggle} label="unparented tasks" />
        <TypeIcon type="task" />
        <span className="flex-1" />
        <span data-testid={ids.rowCount}>{tasks}</span>
      </div>
      <span data-testid={ids.rowTitle} className="text-[length:var(--text-sm)] text-ink">
        Unparented
      </span>
      {collapsed ? null : (
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Tasks with no story or bug. They carry no sprint points.
        </span>
      )}
    </div>
  );
}
