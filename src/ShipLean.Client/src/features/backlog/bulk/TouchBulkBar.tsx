import { useState } from 'react';
import type { StateDto } from '@shiplean/core/api/projects';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { ParentRefDto } from '@shiplean/core/api/workItems';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import { BULK_MAX_ROWS } from '@shiplean/core/api/backlogWrites';
import type { SheetAction } from '../../../touch/ActionSheet';
import { MoreButton } from '../../../touch/MoreButton';
import { backlogTestIds as ids } from '../testIds';
import { BulkParentSheet } from './BulkParentSheet';
import { BulkPointsSheet } from './BulkPointsSheet';
import { BulkSprintSheet, type MoveTarget } from './BulkSprintSheet';
import { BulkStateSheet } from './BulkStateSheet';
import { BulkTagSheet } from './BulkTagSheet';

type Sheet = 'state' | 'points' | 'sprint' | 'parent' | 'tag';

const barButtonClass =
  'min-h-[var(--touch-target)] flex-none rounded-control border border-edge bg-panel px-3 text-[length:var(--text-base)] font-medium text-ink active:bg-signal-soft disabled:opacity-50';

// What the touch bar needs from BulkBar, which owns the selection, the plans and the runner.
export interface TouchBulkBarProps {
  count: number;
  selected: readonly BacklogRowDto[];
  projectId: string;
  targets: readonly MoveTarget[];
  velocity: VelocityDto | null;
  states: readonly StateDto[];
  stateNames: readonly string[];
  parentTypes: WorkItemTypeId[];
  onMoveTo: (target: MoveTarget) => void;
  onMoveToEdge: (slot: 'first' | 'last') => void;
  onSetPoints: (points: number | null) => void;
  onSetState: (name: string) => void;
  onSetParent: (parent: ParentRefDto | null) => void;
  onAddTag: (tag: string) => void;
}

const sumPoints = (rows: readonly BacklogRowDto[]) =>
  rows.reduce((sum, row) => sum + (row.points ?? 0), 0);

/**
 * The bulk bar at touch (SHIP-716 D3): fixed to the bottom above the safe area, with the count and points on the left
 * and State, Points, Sprint and ⋯ (Parent, Tags, Move to top, Move to bottom) on the right, each a K5 sheet. It is not a
 * SaveBar: nothing here is saved, each pick runs. A spacer keeps the list's last row clear of it.
 */
export function TouchBulkBar(props: TouchBulkBarProps) {
  const { count, selected, targets, velocity } = props;
  const [sheet, setSheet] = useState<Sheet | null>(null);
  const close = () => setSheet(null);
  const tooMany = count > BULK_MAX_ROWS;
  const unestimated = selected.filter((row) => row.points === null).length;
  const moreActions: SheetAction[] = [
    {
      id: 'parent',
      label: 'Parent ›',
      disabled: tooMany || props.parentTypes.length === 0,
      onSelect: () => setSheet('parent'),
    },
    { id: 'tag', label: 'Tags ›', disabled: tooMany, onSelect: () => setSheet('tag') },
    {
      id: 'top',
      label: 'Move to top',
      disabled: tooMany,
      onSelect: () => props.onMoveToEdge('first'),
    },
    {
      id: 'bottom',
      label: 'Move to bottom',
      disabled: tooMany,
      onSelect: () => props.onMoveToEdge('last'),
    },
  ];
  return (
    <>
      <div aria-hidden="true" className="h-[calc(4rem+env(safe-area-inset-bottom))] flex-none" />
      <div
        data-testid={ids.bulkBar}
        role="toolbar"
        aria-label="Actions for the selected rows"
        className="fixed inset-x-0 bottom-0 z-[45] flex items-center gap-1.5 border-t-2 border-signal bg-panel px-2 pt-2 pb-[calc(0.5rem+env(safe-area-inset-bottom))] shadow-[0_-6px_18px_rgb(0_0_0/0.08)]"
      >
        <div className="grid min-w-0 flex-1 leading-tight">
          <b data-testid={ids.bulkCount} className="truncate text-[length:var(--text-base)]">
            {count} selected
          </b>
          <span className="truncate text-[length:var(--text-xs)] text-ink-muted">
            {sumPoints(selected)} pts
            {unestimated > 0 ? ` · ${unestimated} not estimated` : ''}
          </span>
        </div>
        {tooMany ? (
          <span
            data-testid={ids.bulkLimit}
            className="text-[length:var(--text-xs)] font-semibold text-caution"
          >
            {BULK_MAX_ROWS} rows or fewer
          </span>
        ) : null}
        <button
          type="button"
          data-testid={ids.bulkTouchState}
          disabled={tooMany}
          className={barButtonClass}
          onClick={() => setSheet('state')}
        >
          State
        </button>
        <button
          type="button"
          data-testid={ids.bulkTouchPoints}
          disabled={tooMany}
          className={barButtonClass}
          onClick={() => setSheet('points')}
        >
          Points
        </button>
        <button
          type="button"
          data-testid={ids.bulkTouchSprint}
          disabled={tooMany}
          className={barButtonClass}
          onClick={() => setSheet('sprint')}
        >
          Sprint
        </button>
        <MoreButton actions={moreActions} label="More for the selection" />
      </div>
      {sheet === 'state' ? (
        <BulkStateSheet
          names={props.stateNames}
          states={props.states}
          onPick={props.onSetState}
          onClose={close}
        />
      ) : null}
      {sheet === 'points' ? <BulkPointsSheet onSet={props.onSetPoints} onClose={close} /> : null}
      {sheet === 'sprint' ? (
        <BulkSprintSheet
          targets={targets}
          selected={selected}
          velocity={velocity}
          disabled={tooMany}
          onPick={props.onMoveTo}
          onClose={close}
        />
      ) : null}
      {sheet === 'parent' ? (
        <BulkParentSheet
          projectId={props.projectId}
          types={props.parentTypes}
          onPick={props.onSetParent}
          onClose={close}
        />
      ) : null}
      {sheet === 'tag' ? <BulkTagSheet onAdd={props.onAddTag} onClose={close} /> : null}
    </>
  );
}
