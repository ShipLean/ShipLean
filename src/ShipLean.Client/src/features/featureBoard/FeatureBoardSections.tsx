import type { UserRef } from '../../api/refs';
import type { FeatureBoardViewDto } from '../../api/views';
import { TaskboardGrid } from '../taskboard/grid/TaskboardGrid';
import type { TaskboardGrouping } from '../taskboard/taskboardApi';
import type { TaskboardScope } from '../taskboard/taskboardScope';
import { featureBoardLayout, sectionDomId } from './featureBoardLayout';
import { FeatureSectionHeader } from './FeatureSectionHeader';
import { featureBoardTestIds as ids } from './testIds';

/**
 * The board's grids. A leaf Feature, or the People view of any tree, is one grid; a parent Feature grouped by Stories gets one
 * grid per Feature in its tree, each under a header that collapses it. All sit in the page's one drag context.
 */
export function FeatureBoardSections({
  projectKey,
  projectId,
  scope,
  view,
  grouping,
  faces,
  me,
  closedSections,
  onToggleSection,
  isCollapsed,
  onToggle,
  onSetState,
}: {
  projectKey: string;
  projectId: string;
  scope: TaskboardScope;
  view: FeatureBoardViewDto;
  grouping: TaskboardGrouping;
  faces: ReadonlySet<string>;
  me: UserRef | undefined;
  closedSections: ReadonlySet<string>;
  onToggleSection: (sectionId: string) => void;
  isCollapsed: (rowId: string, completed: boolean) => boolean;
  onToggle: (rowId: string, completed: boolean) => void;
  onSetState: (parentId: string, key: string, revision: number, stateId: string) => void;
}) {
  const grid = (rows: FeatureBoardViewDto['rows']) => (
    <TaskboardGrid
      projectKey={projectKey}
      projectId={projectId}
      scope={scope}
      view={view}
      rows={rows}
      grouping={grouping}
      faces={faces}
      me={me}
      isCollapsed={isCollapsed}
      onToggle={onToggle}
      onSetState={onSetState}
    />
  );
  const layout = featureBoardLayout(view);
  if (grouping === 'people' || layout.length === 0) return grid(view.rows);
  return (
    <>
      {layout.map(({ section, rows, direct, empty }) => {
        const closed = closedSections.has(section.id);
        return (
          <section
            key={section.id}
            id={sectionDomId(section.id)}
            data-testid={ids.section}
            data-section={section.key}
            className="border-t border-edge first:border-t-0"
          >
            <FeatureSectionHeader
              projectKey={projectKey}
              section={section}
              direct={direct}
              collapsed={closed}
              onToggle={() => onToggleSection(section.id)}
            />
            {closed ? null : empty ? (
              <p
                data-testid={ids.sectionEmpty}
                className="m-0 bg-panel py-2 pr-4 text-[length:var(--text-sm)] text-ink-muted"
                style={{
                  paddingLeft: `calc(var(--space-4) + 28px + ${Math.max(section.depth - 1, 0) * 22}px)`,
                }}
              >
                No stories or bugs yet.
              </p>
            ) : (
              grid(rows)
            )}
          </section>
        );
      })}
    </>
  );
}
