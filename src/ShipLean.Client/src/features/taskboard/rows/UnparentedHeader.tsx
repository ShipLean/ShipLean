import { TypeIcon } from '../../../components/TypeIcon';
import { taskboardTestIds as ids } from '../testIds';
import { RowToggle } from './RowToggle';

/** The header of the Unparented row: tasks with no story or bug. */
export function UnparentedHeader({
  count,
  collapsed,
  onToggle,
}: {
  count: number;
  collapsed: boolean;
  onToggle: () => void;
}) {
  return (
    <div data-testid={ids.rowHeader} className="grid gap-1 px-2 py-2">
      <div className="flex items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <RowToggle collapsed={collapsed} onToggle={onToggle} label="unparented tasks" />
        <TypeIcon type="task" />
        <span className="flex-1" />
        <span data-testid={ids.rowCount}>
          {count} {count === 1 ? 'task' : 'tasks'}
        </span>
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
