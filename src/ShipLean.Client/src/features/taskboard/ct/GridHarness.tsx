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
}: {
  rowIndexes: number[];
  /** Puts the grid in a scroller this tall, as the shell's main area is. */
  scrollerHeight?: number;
}) {
  const grid = (
    <TaskboardDnd
      projectId={PROJECT.id}
      scope={scope}
      view={VIEW}
      grouping="stories"
      me={undefined}
      onLanded={() => undefined}
    >
      <TaskboardGrid
        projectKey={PROJECT.key}
        projectId={PROJECT.id}
        scope={scope}
        view={VIEW}
        rows={rowIndexes.map((i) => VIEW.rows[i])}
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
