import { TaskboardDnd } from '../moves/TaskboardDnd';
import { TaskboardGrid } from '../grid/TaskboardGrid';
import type { TaskboardScope } from '../taskboardScope';
import { PROJECT, VIEW } from './fixtures';

const scope: TaskboardScope = { kind: 'feature', featureId: 'f1' };
const NO_FACES: ReadonlySet<string> = new Set();

/** A bare grid in its drag context, drawing only the VIEW rows at these indexes (a mount cannot carry functions or a Set). */
export function GridHarness({
  rowIndexes,
  scrollerHeight,
  extraColumns = 0,
}: {
  rowIndexes: number[];
  /** Puts the grid in a scroller this tall, as the shell's main area is. */
  scrollerHeight?: number;
  /** Empty task states added after VIEW's three, to make the grid wider than a desktop screen. */
  extraColumns?: number;
}) {
  const view: typeof VIEW = {
    ...VIEW,
    columns: [
      ...VIEW.columns,
      ...Array.from({ length: extraColumns }, (_, i) => ({
        state: { id: `t-extra-${i}`, name: `Extra ${i + 1}`, category: 'inProgress' as const },
        count: 0,
      })),
    ],
  };
  const grid = (
    <TaskboardDnd
      projectId={PROJECT.id}
      scope={scope}
      view={view}
      grouping="stories"
      me={undefined}
      onLanded={() => undefined}
    >
      <TaskboardGrid
        projectKey={PROJECT.key}
        projectId={PROJECT.id}
        scope={scope}
        view={view}
        rows={rowIndexes.map((i) => view.rows[i])}
        grouping="stories"
        faces={NO_FACES}
        me={undefined}
        isCollapsed={() => false}
        onToggle={() => undefined}
        onSetState={() => undefined}
      />
    </TaskboardDnd>
  );
  return scrollerHeight === undefined ? (
    grid
  ) : (
    <div data-testid="scroller" className="overflow-auto" style={{ height: scrollerHeight }}>
      {grid}
    </div>
  );
}
