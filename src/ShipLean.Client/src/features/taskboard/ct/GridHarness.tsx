import { TaskboardDnd } from '../moves/TaskboardDnd';
import { TaskboardGrid } from '../grid/TaskboardGrid';
import type { TaskboardScope } from '../taskboardScope';
import { PROJECT, VIEW } from './fixtures';

const scope: TaskboardScope = { kind: 'feature', featureId: 'f1' };
const NO_FACES: ReadonlySet<string> = new Set();

/** A bare grid in its drag context, drawing only the VIEW rows at these indexes (a mount cannot carry functions or a Set). */
export function GridHarness({ rowIndexes }: { rowIndexes: number[] }) {
  return (
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
}
