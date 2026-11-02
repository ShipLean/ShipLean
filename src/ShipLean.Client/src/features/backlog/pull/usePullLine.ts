import { useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react';
import { BULK_MAX_ROWS } from '../../../api/backlogWrites';
import type { SprintRef } from '../../../api/refs';
import type { VelocityDto } from '../../../api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import { moveToSprintPlan } from '../bulk/movePlan';
import { sprintSum, type SprintSum } from '../bulk/sprintSum';
import { useBulkRunner } from '../bulk/useBulkRun';
import { pullCut } from './pullCut';

/** Where the pull line sits in the Backlog section, and what Pull would do. */
export interface PullLine {
  /** The row the line is drawn under; null for no line, or for a line above the first row (`atTop`). */
  afterRowId: string | null;
  /** The cut is zero rows: the line is drawn above the first row. */
  atTop: boolean;
  /** The row falls above the line, so the Pull action would bring it into the sprint. */
  isPulled: (rowId: string) => boolean;
  /** The future sprints the line can fill, in order. Only real sprints: the forecast's hypothetical one has no id. */
  targets: readonly SprintRef[];
  target: SprintRef | null;
  chooseTarget: (sprintId: string) => void;
  /** How many rows Pull would take: the rows above the line, at most BULK_MAX_ROWS. */
  count: number;
  /** The rows above the line that have no estimate and count 0. */
  unestimated: number;
  /** The sprint after the rows above the line join it, in the app's one format, with " · capped" and " · +n over". */
  sum: SprintSum | null;
  sumText: string;
  /** The line has moved off what fits: Back to what fits shows. */
  moved: boolean;
  reset: () => void;
  pull: () => Promise<void>;
  /** The grip's handlers: dragging snaps below the row under the pointer; Alt+Up and Alt+Down move one row. */
  grip: {
    onPointerDown: (event: PointerEvent<HTMLElement>) => void;
    onKeyDown: (event: KeyboardEvent<HTMLElement>) => void;
  };
  /** True once after a keyboard move, so the remounted grip takes focus back. */
  takeFocus: () => boolean;
}

export interface PullLineInput {
  section: BacklogSectionDto;
  /** The sprint sections: the line fills one of the future ones. */
  sprints: readonly BacklogSectionDto[];
  velocity: VelocityDto | null;
  forecast: boolean;
  projectId: string;
  /** A filter, search or check queue is on: the line is off while rows are hidden. */
  filtering: boolean;
}

const NOTHING: ReadonlySet<string> = new Set();

const roomOf = (section: BacklogSectionDto, velocity: VelocityDto | null) =>
  sprintSum(section, [], velocity).of ?? 0;

function rowsBelow(y: number, rows: Element[]): number {
  let n = 0;
  for (const row of rows) {
    const box = row.getBoundingClientRect();
    if (y > box.top + box.height / 2) n++;
    else break;
  }
  return n;
}

/** The forecast's cut as a line in the Backlog, which Pull moves above-the-line rows into the next sprint. */
export function usePullLine({
  section,
  sprints,
  velocity,
  forecast,
  filtering,
}: PullLineInput): PullLine {
  const bulk = useBulkRunner();
  const [targetId, setTargetId] = useState<string | null>(null);
  const [cut, setCut] = useState<number | null>(null);
  const refocus = useRef(false);

  const rows = section.rows;
  const futures = useMemo(
    () => sprints.filter((s) => s.sprint !== null && s.sprint.status === 'future'),
    [sprints],
  );
  const stack = useMemo(() => rows.map((row) => row.points), [rows]);
  const fits = (sprint: BacklogSectionDto) =>
    pullCut(stack, roomOf(sprint, velocity), sprint.points);
  const chosen = futures.find((s) => s.id === targetId);
  const target = chosen ?? futures.find((s) => fits(s).selected > 0) ?? futures[0] ?? null;
  const auto = target === null ? null : fits(target);

  const on = forecast && !filtering && target !== null && auto !== null && rows.length > 0;
  const n = !on ? 0 : Math.min(cut ?? auto.selected, rows.length);
  const taken: BacklogRowDto[] = rows.slice(0, Math.min(n, BULK_MAX_ROWS));
  const capped = on && (n > BULK_MAX_ROWS || (cut === null && auto.capped));
  const pulledIds = useMemo(
    () => (on ? new Set(rows.slice(0, Math.min(n, BULK_MAX_ROWS)).map((r) => r.id)) : NOTHING),
    [on, rows, n],
  );
  const sum = on ? sprintSum(target, taken, velocity) : null;
  const sumText =
    sum === null
      ? ''
      : [
          sum.text,
          capped ? `capped at ${BULK_MAX_ROWS} rows` : null,
          sum.over && sum.of !== null ? `+${sum.after - sum.of} over` : null,
        ]
          .filter((part) => part !== null)
          .join(' · ');

  const move = (next: number) => setCut(Math.max(0, Math.min(rows.length, next)));
  const reset = () => setCut(null);

  function onPointerDown(event: PointerEvent<HTMLElement>) {
    if (!on) return;
    event.preventDefault();
    const body = event.currentTarget.closest('tbody');
    if (body === null) return;
    document.body.classList.add('pulling');
    const track = (pointer: globalThis.PointerEvent) => {
      const shown = [...body.querySelectorAll(`tr[data-section="${section.id}"][data-row-id]`)];
      move(rowsBelow(pointer.clientY, shown));
    };
    const release = () => {
      document.removeEventListener('pointermove', track);
      document.removeEventListener('pointerup', release);
      document.body.classList.remove('pulling');
    };
    document.addEventListener('pointermove', track);
    document.addEventListener('pointerup', release);
  }

  function onKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (!on || !event.altKey || (event.key !== 'ArrowUp' && event.key !== 'ArrowDown')) return;
    event.preventDefault();
    event.stopPropagation();
    refocus.current = true;
    move(n + (event.key === 'ArrowDown' ? 1 : -1));
  }

  async function pull() {
    if (!on || taken.length === 0) return;
    const outcome = await bulk.run(moveToSprintPlan(taken, target.sprint));
    if (outcome.failed !== true) {
      setCut(null);
      setTargetId(null);
    }
  }

  return {
    afterRowId: on && n > 0 ? rows[n - 1].id : null,
    atTop: on && n === 0,
    isPulled: (rowId) => pulledIds.has(rowId),
    targets: futures.flatMap((s) => (s.sprint === null ? [] : [s.sprint])),
    target: on ? target.sprint : null,
    chooseTarget: (sprintId) => {
      setTargetId(sprintId);
      setCut(null);
    },
    count: taken.length,
    unestimated: taken.filter((row) => row.points === null).length,
    sum,
    sumText,
    moved: on && cut !== null,
    reset,
    pull,
    grip: { onPointerDown, onKeyDown },
    takeFocus: () => {
      const wanted = refocus.current;
      refocus.current = false;
      return wanted;
    },
  };
}
