import { useContext, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { SprintRef } from '@shiplean/core/api/refs';
import type { ParentRefDto } from '@shiplean/core/api/workItems';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { MoreButton } from '../../../touch/MoreButton';
import type { SheetAction } from '../../../touch/ActionSheet';
import { parentTypesOf } from '../../workItem/form/ParentField';
import { useItemPeek } from '../../itemPeek/useItemPeek';
import { moveToBacklogEdgePlan, moveToEdgePlan, moveToSprintPlan } from '../bulk/movePlan';
import { setParentPlan } from '../bulk/setPlans';
import { BulkRunContext, useBulkRunner } from '../bulk/useBulkRun';
import { activeBacklogView, cachedRow } from '../estimate/cachedRow';
import { useEstimate } from '../estimate/useEstimate';
import { SelectionContext, useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { PointsSheet } from './PointsSheet';
import { RowParentSheet } from './RowParentSheet';
import { RowSprintSheet } from './RowSprintSheet';
import { useTouchSelectMode } from './selectMode';

type Sub = 'points' | 'sprint' | 'parent';

function Points({
  row,
  shown,
  onOpen,
}: {
  row: BacklogRowDto;
  shown: number | null;
  onOpen: (() => void) | null;
}) {
  if (shown !== null) {
    return (
      <span data-testid={ids.points} className="font-semibold tabular-nums">
        {shown}
      </span>
    );
  }
  if (onOpen === null) {
    return (
      <span data-testid={ids.points} title="Not estimated" className="font-semibold text-caution">
        ?
      </span>
    );
  }
  return (
    <button
      type="button"
      data-testid={ids.points}
      aria-label={`Estimate ${row.key}`}
      title="Not estimated"
      className="inline-grid min-h-[var(--touch-target)] min-w-[var(--touch-target)] place-items-center rounded-control font-semibold text-caution active:bg-signal-soft"
      onClick={onOpen}
    >
      ?
    </button>
  );
}

// The row's one-row writes, as today's bulk plans: the same K1 request and Undo toast as the bar and the drag.
function MoreActions({
  row,
  open,
  setSub,
}: {
  row: BacklogRowDto;
  open: () => void;
  setSub: (sub: Sub) => void;
}) {
  const runner = useBulkRunner();
  const queryClient = useQueryClient();
  const selectMode = useTouchSelectMode();
  const selection = useSelection();
  const selectable = useContext(SelectionContext) !== null;
  // Planning's top and bottom also take a row out of its sprint, as its bar does; the read holding the row says which.
  const planning = () => activeBacklogView(queryClient, row.id)?.mode === 'planning';
  const edge = (slot: 'first' | 'last') =>
    void runner.run(planning() ? moveToBacklogEdgePlan([row], slot) : moveToEdgePlan([row], slot));
  const actions: SheetAction[] = [
    { id: 'open', label: 'Open', onSelect: open },
    ...(selectable
      ? [{ id: 'points', label: 'Points', onSelect: () => setSub('points') } satisfies SheetAction]
      : []),
    { id: 'sprint', label: 'Sprint ›', onSelect: () => setSub('sprint') },
    {
      id: 'parent',
      label: 'Parent ›',
      onSelect: () => setSub('parent'),
      disabled: row.type === null || parentTypesOf(row.type.id).length === 0,
    },
    { id: 'top', label: 'Move to top', onSelect: () => edge('first') },
    { id: 'bottom', label: 'Move to bottom', onSelect: () => edge('last') },
    ...(selectable
      ? [
          {
            id: 'select',
            label: 'Select',
            onSelect: () => {
              selectMode.start();
              selection.toggle(row.id);
            },
          } satisfies SheetAction,
        ]
      : []),
  ];
  return <MoreButton actions={actions} label={`More for ${row.key}`} />;
}

/**
 * The right edge of a touch row (SHIP-716 D9, Row ⋯): the points (a ? chip opening the Points sheet while there is no
 * estimate), then `children` (the assignee), then the ⋯ with Open, Points, Sprint ›, Parent ›, Move to top, Move to
 * bottom and Select. The ⋯ needs a BulkRunProvider (List, Planning, Tree) and is off while selecting; Points and
 * Select need the SelectionProvider, so Tree rows keep a display-only ?.
 */
export function RowActions({ row, children }: { row: BacklogRowDto; children?: ReactNode }) {
  const queryClient = useQueryClient();
  const peek = useItemPeek();
  const estimate = useEstimate(row);
  const { selecting } = useTouchSelectMode();
  const [sub, setSub] = useState<Sub | null>(null);
  const projectId = cachedRow(queryClient, row.id)?.projectId ?? null;
  const runner = useContext(BulkRunContext);
  const close = () => setSub(null);
  const pointsOpen = sub === 'points' || estimate.conflict !== null;
  return (
    <>
      <Points
        row={row}
        shown={estimate.shown}
        onOpen={estimate.editable && !selecting ? () => setSub('points') : null}
      />
      {children}
      {runner !== null && !selecting && projectId !== null ? (
        <MoreActions row={row} open={() => peek.open(row.key)} setSub={setSub} />
      ) : null}
      {estimate.editable ? (
        <PointsSheet
          row={row}
          estimate={estimate}
          open={pointsOpen}
          onClose={() => {
            if (estimate.conflict !== null) estimate.keepTheirs();
            close();
          }}
        />
      ) : null}
      {sub === 'sprint' && projectId !== null && runner !== null ? (
        <RowSprintSheet
          row={row}
          projectId={projectId}
          onPick={(sprint: SprintRef | null) => void runner.run(moveToSprintPlan([row], sprint))}
          onClose={close}
        />
      ) : null}
      {sub === 'parent' && projectId !== null && runner !== null ? (
        <RowParentSheet
          row={row}
          projectId={projectId}
          onPick={(parent: ParentRefDto | null) =>
            void runner.run(
              setParentPlan([row], parent === null ? null : { id: parent.id, title: parent.title })
                .plan,
            )
          }
          onClose={close}
        />
      ) : null}
    </>
  );
}
