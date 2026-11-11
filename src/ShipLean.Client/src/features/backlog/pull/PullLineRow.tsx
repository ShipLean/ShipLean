import { useLayoutEffect, useRef } from 'react';
import { CapacityBar } from '../../../components/CapacityBar';
import { Chip } from '../../../components/Chip';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import { backlogTestIds as ids } from '../testIds';
import type { PullLine } from './usePullLine';

// The pull line, drawn under the row where the forecast's cut falls: what Everything above it would do to the next
// sprint, a grip to move it, and Pull. Going over is a warning, never a block.
export function PullLineRow({
  line,
  columnCount,
}: {
  line: PullLine;
  row: BacklogRowDto;
  section: BacklogSectionDto;
  projectId: string;
  columnCount: number;
}) {
  const grip = useRef<HTMLButtonElement>(null);
  const { takeFocus } = line;
  useLayoutEffect(() => {
    if (takeFocus()) grip.current?.focus();
  }, [takeFocus]);
  const { sum, target } = line;
  if (sum === null || target === null) return null;
  const limit = sum.of;
  return (
    <tr data-testid={ids.pullLine} data-over={sum.over}>
      <td
        data-testid={ids.pullCell}
        colSpan={columnCount}
        className="h-[42px] border-y-2 border-accent bg-accent-soft px-2 text-[length:var(--text-sm)]"
      >
        <div data-testid={ids.pullContent} className="flex flex-wrap items-center gap-2">
          <button
            ref={grip}
            type="button"
            data-testid={ids.pullGrip}
            aria-label="Move the pull line. Drag, or Alt with the up and down arrows"
            className="inline-grid h-6 w-[26px] cursor-ns-resize touch-none place-items-center rounded-control border border-accent bg-panel text-accent focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent"
            {...line.grip}
          >
            ⠿
          </button>
          <span>
            Everything above this line,{' '}
            <b>
              {line.count} item{line.count === 1 ? '' : 's'}
            </b>
            , into
          </span>
          <select
            data-testid={ids.pullTarget}
            className="field h-6"
            aria-label="Sprint to pull into"
            value={target.id}
            onChange={(event) => line.chooseTarget(event.target.value)}
          >
            {line.targets.map((sprint) => (
              <option key={sprint.id} value={sprint.id}>
                {sprint.name}
              </option>
            ))}
          </select>
          {limit === null ? null : (
            <span className="w-[90px]">
              <CapacityBar used={sum.after} capacity={limit} />
            </span>
          )}
          <span
            data-testid={ids.pullSum}
            data-over={sum.over}
            className={sum.over ? 'font-semibold text-alert' : 'font-semibold'}
          >
            {line.sumText}
          </span>
          {line.unestimated > 0 ? (
            <Chip tone="warn">{line.unestimated} not estimated, counted as 0</Chip>
          ) : null}
          {line.moved ? (
            <button
              type="button"
              data-testid={ids.pullReset}
              className="btn-ghost"
              onClick={line.reset}
            >
              Back to what fits
            </button>
          ) : null}
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.pullGo}
            className="btn-signal"
            disabled={line.count === 0}
            onClick={() => void line.pull()}
          >
            Pull into {target.name}
          </button>
        </div>
      </td>
    </tr>
  );
}
