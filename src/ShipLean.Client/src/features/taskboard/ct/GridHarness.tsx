import { CardFeatureContext } from '../cards/CardFeatureContext';
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
  retitle = {},
  peopleFeatureTitle,
  extraColumns = 0,
}: {
  rowIndexes: number[];
  /** Puts the grid in a scroller this tall, as the shell's main area is. */
  scrollerHeight?: number;
  /** New titles for tasks, by key. */
  retitle?: Record<string, string>;
  /** Groups by People and gives every card's story this Feature title, as the Feature board does. */
  peopleFeatureTitle?: string;
  /** Empty task states added after VIEW's three, to make the grid wider than a desktop screen. */
  extraColumns?: number;
}) {
  const grouping = peopleFeatureTitle === undefined ? 'stories' : 'people';
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
  const rows = rowIndexes
    .map((i) => (grouping === 'people' ? VIEW.peopleRows[i] : VIEW.rows[i]))
    .map((row) => ({
      ...(row as (typeof VIEW.rows)[number]),
      cells: row.cells.map((cell) => ({
        ...cell,
        tasks: cell.tasks.map((t) => ({ ...t, title: retitle[t.key] ?? t.title })),
      })),
    }));
  const grid = (
    <TaskboardDnd
      projectId={PROJECT.id}
      scope={scope}
      view={view}
      grouping={grouping}
      me={undefined}
      onLanded={() => undefined}
    >
      <TaskboardGrid
        projectKey={PROJECT.key}
        projectId={PROJECT.id}
        scope={scope}
        view={view}
        rows={rows}
        grouping={grouping}
        faces={NO_FACES}
        me={undefined}
        isCollapsed={() => false}
        onToggle={() => undefined}
        onSetState={() => undefined}
      />
    </TaskboardDnd>
  );
  const features =
    peopleFeatureTitle === undefined
      ? null
      : new Map(
          VIEW.peopleRows
            .flatMap((r) => r.cells.flatMap((c) => c.tasks))
            .flatMap((t) => (t.parent ? [t.parent.id] : []))
            .map((id) => [id, { key: 'SHIP-1', title: peopleFeatureTitle }] as const),
        );
  const content = (
    <CardFeatureContext.Provider value={features}>{grid}</CardFeatureContext.Provider>
  );
  return scrollerHeight === undefined ? (
    content
  ) : (
    <div data-testid="scroller" className="overflow-auto" style={{ height: scrollerHeight }}>
      {content}
    </div>
  );
}
