import { useMemo, useState } from 'react';
import { showToast } from '../../../components/toastStore';
import type { SprintRef, WorkItemTypeId } from '../../../api/refs';
import { BULK_MAX_ROWS } from '../../../api/backlogWrites';
import type { BacklogRowDto, BacklogSectionDto, BacklogViewDto } from '../../../api/views';
import { useProjectSprints, useProjectStates } from '../../workItem/workItemApi';
import { parentTypesOf } from '../../workItem/form/ParentField';
import { useListCommand } from '../commands/listCommands';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { BulkMenu } from './BulkMenus';
import { ParentMenuBody } from './ParentMenuBody';
import { PointsMenuBody } from './PointsMenuBody';
import { SprintMoveButton } from './SprintMoveButton';
import { StateMenuBody } from './StateMenuBody';
import { TagMenuBody } from './TagMenuBody';
import { moveToBacklogEdgePlan, moveToEdgePlan, moveToSprintPlan } from './movePlan';
import {
  removePlan,
  setParentPlan,
  setPointsPlan,
  setStatePlan,
  setTagPlan,
  stateNamesFor,
  type SetPlan,
} from './setPlans';
import { sprintSum } from './sprintSum';
import { useBulkRunner, type BulkPlan } from './useBulkRun';

/** The backlog as the screen read it (`undefined` while it has not been), and the project, for the bar's actions. */
export interface BulkBarProps {
  mode: 'list' | 'planning';
  projectId: string;
  projectKey: string;
  view: BacklogViewDto | undefined;
}

interface MoveTarget {
  sprint: SprintRef;
  section: BacklogSectionDto;
}

const rankOf = (row: BacklogRowDto) => row.rank ?? Number.MAX_SAFE_INTEGER;

function allRows(view: BacklogViewDto | undefined): BacklogRowDto[] {
  if (view === undefined) return [];
  return [...view.rows, ...view.sections.flatMap((section) => section.rows)].sort(
    (a, b) => rankOf(a) - rankOf(b),
  );
}

const sumPoints = (rows: readonly BacklogRowDto[]) =>
  rows.reduce((sum, row) => sum + (row.points ?? 0), 0);

// The action bar over a selection: move, set, remove. Nothing here assigns: work is pulled.
export function BulkBar({ mode, projectId, view }: BulkBarProps) {
  const selection = useSelection();
  const runner = useBulkRunner();
  const states = useProjectStates(projectId).data ?? [];
  const sprints = useProjectSprints(projectId).data ?? [];
  const [moving, setMoving] = useState(false);

  const rows = useMemo(() => allRows(view), [view]);
  const selected = selection.selectedRows(rows);
  const count = selection.count;
  const tooMany = count > BULK_MAX_ROWS;

  // Planning moves into its own sections; the list has none, so a sprint holds what the shown rows put in it.
  const targets: MoveTarget[] =
    mode === 'planning'
      ? (view?.sections ?? []).flatMap((section) =>
          section.sprint === null || section.sprint.status === 'past'
            ? []
            : [{ sprint: section.sprint, section }],
        )
      : sprints
          .filter((sprint) => sprint.status !== 'past')
          .map((sprint) => ({
            sprint: { id: sprint.id, name: sprint.name, status: sprint.status },
            section: {
              id: sprint.id,
              title: sprint.name,
              sprint,
              points: sumPoints(rows.filter((row) => row.sprint?.id === sprint.id)),
              capacity: null,
              rows: [],
            },
          }));

  async function apply(set: SetPlan | { plan: BulkPlan; skipped: null }, clearFirst = false) {
    setMoving(false);
    if (set.plan.items.length === 0) {
      showToast(set.skipped ?? 'Nothing to change', { tone: 'warn' });
      return;
    }
    // Clear before the run: the outcome host re-selects the rows a teammate changed once it resolves. A failed run
    // wrote nothing, so the user gets their selection back.
    const before = clearFirst ? [...selection.ids] : [];
    if (clearFirst) selection.clear();
    const outcome = await runner.run(set.plan);
    if (clearFirst && outcome.failed === true) selection.addMany(before);
  }

  const moveTo = (target: MoveTarget) =>
    apply({ plan: moveToSprintPlan(selected, target.sprint), skipped: null });
  const moveToEdge = (slot: 'first' | 'last') =>
    apply({
      plan:
        mode === 'planning'
          ? moveToBacklogEdgePlan(selected, slot)
          : moveToEdgePlan(selected, slot),
      skipped: null,
    });

  useListCommand('moveMenu', (arg) => {
    if (count === 0 || tooMany) return;
    if (arg === undefined) {
      setMoving(true);
      return;
    }
    const key = String(arg).toLowerCase();
    if (key === 't') void moveToEdge('first');
    else if (key === 'b') void moveToEdge('last');
    else if (targets[Number(key) - 1] !== undefined) void moveTo(targets[Number(key) - 1]);
    else setMoving(false);
  });

  const unestimated = selected.filter((row) => row.points === null).length;
  // Only parents every selected type may have.
  const parentTypes = selected.reduce<WorkItemTypeId[] | null>((common, row) => {
    const types = row.type === null ? [] : parentTypesOf(row.type.id);
    return common === null ? types : common.filter((type) => types.includes(type));
  }, null);

  return (
    <>
      <span role="status" aria-live="polite" className="sr-only">
        {count === 0 ? '' : `${count} selected`}
      </span>
      {count === 0 ? null : (
        <div
          data-testid={ids.bulkBar}
          role="toolbar"
          aria-label="Actions for the selected rows"
          className="sticky bottom-0 z-[5] flex flex-none flex-wrap items-center gap-2 border-t-2 border-signal bg-panel px-4 py-2 shadow-[0_-6px_18px_rgb(0_0_0/0.08)]"
        >
          <div className="grid leading-tight whitespace-nowrap">
            <b data-testid={ids.bulkCount} className="text-[length:var(--text-md)]">
              {count} selected
            </b>
            <span className="text-[length:var(--text-xs)] text-ink-muted">
              {sumPoints(selected)} pts
              {unestimated > 0 ? (
                <>
                  {' · '}
                  <span className="font-semibold text-caution">{unestimated} not estimated</span>
                </>
              ) : null}
            </span>
          </div>
          <button
            type="button"
            data-testid={ids.bulkClear}
            className="btn-ghost"
            title="Clear the selection"
            onClick={selection.clear}
          >
            Clear <kbd className="ml-1 font-mono text-[length:var(--text-2xs)]">Esc</kbd>
          </button>
          {tooMany ? (
            <span
              data-testid={ids.bulkLimit}
              className="text-[length:var(--text-sm)] font-semibold text-caution"
            >
              Select {BULK_MAX_ROWS} rows or fewer to act on them.
            </span>
          ) : null}
          <span aria-hidden="true" className="w-px self-stretch bg-edge" />
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
              Move to
            </span>
            {targets.map((target, n) => (
              <SprintMoveButton
                key={target.sprint.id}
                name={target.sprint.name}
                current={target.sprint.status === 'current'}
                sum={sprintSum(target.section, selected, view?.velocity ?? null)}
                hint={moving ? n + 1 : undefined}
                disabled={tooMany}
                onMove={() => void moveTo(target)}
              />
            ))}
            <button
              type="button"
              data-testid={ids.bulkMoveTop}
              disabled={tooMany}
              className="btn-ghost disabled:opacity-50"
              onClick={() => void moveToEdge('first')}
            >
              {moving ? <kbd className="mr-1 font-mono">T</kbd> : null}Top
            </button>
            <button
              type="button"
              data-testid={ids.bulkMoveBottom}
              disabled={tooMany}
              className="btn-ghost disabled:opacity-50"
              onClick={() => void moveToEdge('last')}
            >
              {moving ? <kbd className="mr-1 font-mono">B</kbd> : null}Bottom
            </button>
          </div>
          <span aria-hidden="true" className="w-px self-stretch bg-edge" />
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
              Set
            </span>
            <BulkMenu testId={ids.bulkSetPoints} label="Points" disabled={tooMany}>
              {(close) => (
                <PointsMenuBody
                  onSet={(points) => {
                    close();
                    void apply(setPointsPlan(selected, points));
                  }}
                />
              )}
            </BulkMenu>
            <BulkMenu testId={ids.bulkSetState} label="State" disabled={tooMany}>
              {(close) => (
                <StateMenuBody
                  names={stateNamesFor(selected, states)}
                  states={states}
                  onPick={(name) => {
                    close();
                    void apply(setStatePlan(selected, states, name));
                  }}
                />
              )}
            </BulkMenu>
            <BulkMenu testId={ids.bulkSetParent} label="Parent" disabled={tooMany}>
              {(close) => (
                <ParentMenuBody
                  projectId={projectId}
                  types={parentTypes ?? []}
                  onPick={(parent) => {
                    close();
                    void apply(setParentPlan(selected, parent));
                  }}
                />
              )}
            </BulkMenu>
            <BulkMenu testId={ids.bulkSetTag} label="Tag" disabled={tooMany}>
              {(close) => (
                <TagMenuBody
                  onAdd={(tag) => {
                    close();
                    void apply(setTagPlan(selected, tag));
                  }}
                />
              )}
            </BulkMenu>
            <button
              type="button"
              data-testid={ids.bulkRemove}
              disabled={tooMany}
              className="btn-ghost disabled:opacity-50"
              onClick={() => void apply(removePlan(selected, states), true)}
            >
              Remove
            </button>
          </div>
        </div>
      )}
    </>
  );
}
