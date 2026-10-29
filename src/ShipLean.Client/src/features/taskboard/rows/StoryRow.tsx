import type { BoardTypeStatesDto } from '../../../api/boards';
import type { StateRef } from '../../../api/refs';
import type { TaskboardRowDto, TaskboardViewDto } from '../../../api/views';
import { passesFaces } from '../grid/faceFilter';
import { gridColumns } from '../grid/gridColumns';
import type { TaskboardScope } from '../taskboardScope';
import { taskboardTestIds as ids } from '../testIds';
import { AddTaskBox } from './AddTaskBox';
import { CollapsedCell } from './CollapsedCell';
import { StoryRowHeader } from './StoryRowHeader';
import { UnparentedHeader } from './UnparentedHeader';
import { TaskCell } from './TaskCell';

// What a story or bug header shows when the board has no setting for its type.
const ALL_FIELDS = {
  showKey: true,
  showPoints: true,
  showAssignee: true,
  showEmptyFields: false,
  extraFields: ['state'],
};

/** A story or bug row, or the Unparented row (`row.parent` null): the header, then one cell per Task state. */
export function StoryRow({
  projectKey,
  projectId,
  scope,
  row,
  columns,
  parentStates,
  view,
  faces,
  collapsed,
  onToggle,
  onSetState,
}: {
  projectKey: string;
  projectId: string;
  scope: TaskboardScope;
  row: TaskboardRowDto;
  columns: { state: StateRef }[];
  parentStates: BoardTypeStatesDto[];
  view: Pick<TaskboardViewDto, 'styleRules' | 'cardFields' | 'annotations'>;
  faces: ReadonlySet<string>;
  collapsed: boolean;
  onToggle: () => void;
  onSetState: (stateId: string) => void;
}) {
  const rowId = row.parent?.id ?? 'unparented';
  const fields = view.cardFields.find((f) => f.type === row.parent?.type.id) ?? ALL_FIELDS;
  const resolved = parentStates
    .find((s) => s.type === row.parent?.type.id)
    ?.states.find((state) => state.category === 'resolved');
  return (
    <div
      data-testid={ids.row}
      data-row={rowId}
      data-collapsed={collapsed}
      role="row"
      className="grid border-t border-edge"
      style={{ gridTemplateColumns: gridColumns(columns.length) }}
    >
      {row.parent ? (
        <StoryRowHeader
          projectKey={projectKey}
          parent={row.parent}
          parentStates={parentStates}
          fields={fields}
          showProgress={view.annotations.tasks}
          collapsed={collapsed}
          offerResolve={row.offerResolve}
          onToggle={onToggle}
          onSetState={onSetState}
          onMoveToResolved={resolved ? () => onSetState(resolved.id) : null}
        />
      ) : (
        <UnparentedHeader
          count={row.cells.reduce((n, c) => n + c.tasks.length, 0)}
          collapsed={collapsed}
          onToggle={onToggle}
        />
      )}
      {columns.map((column, index) => {
        const cards = (row.cells.find((c) => c.stateId === column.state.id)?.tasks ?? []).filter(
          (card) => passesFaces(card, faces),
        );
        if (collapsed) return <CollapsedCell key={column.state.id} count={cards.length} />;
        return (
          <TaskCell
            key={column.state.id}
            rowId={rowId}
            stateId={column.state.id}
            cards={cards}
            view={view}
            showParent={false}
          >
            {index === 0 ? (
              <AddTaskBox projectId={projectId} scope={scope} parentId={row.parent?.id ?? null} />
            ) : null}
          </TaskCell>
        );
      })}
    </div>
  );
}
