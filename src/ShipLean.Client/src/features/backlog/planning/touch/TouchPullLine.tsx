import { CapacityBar } from '../../../../components/CapacityBar';
import { Chip } from '../../../../components/Chip';
import type { PullLine } from '../../pull/usePullLine';
import { backlogTestIds as ids } from '../../testIds';

/**
 * The pull line at touch (SHIP-716 D5), drawn under the row where the forecast's cut falls: the grip, what everything
 * above it would do to the next sprint, ▲ ▼ to move it one item, and Pull. The grip drags it too (pointer events, as
 * on the desktop: it has touch-action none, so the page does not scroll under it). Going over is a warning, never a block.
 */
export function TouchPullLine({ line }: { line: PullLine }) {
  const { sum, target } = line;
  if (sum === null || target === null) return null;
  const limit = sum.of;
  const step = 'btn-ghost min-h-[var(--touch-target)] min-w-[var(--touch-target)]';
  return (
    <div
      data-testid={ids.pullLine}
      data-over={sum.over}
      className="border-y-2 border-accent bg-accent-soft px-3 py-2 text-[length:var(--text-sm)]"
    >
      <div data-testid={ids.pullContent} className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          data-testid={ids.pullGrip}
          aria-label="Move the pull line: drag it, or use the up and down buttons"
          className="inline-grid min-h-[var(--touch-target)] min-w-[var(--touch-target)] touch-none place-items-center rounded-control border border-accent bg-panel text-accent"
          {...line.grip}
        >
          ⠿
        </button>
        <span>
          Above this line:{' '}
          <b>
            {line.count} item{line.count === 1 ? '' : 's'}
          </b>{' '}
          into
        </span>
        <select
          data-testid={ids.pullTarget}
          className="field min-h-[var(--touch-target)]"
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
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        {limit === null ? null : (
          <span className="w-24 flex-none">
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
        <span className="flex-1" />
        {line.moved ? (
          <button type="button" data-testid={ids.pullReset} className={step} onClick={line.reset}>
            Back to what fits
          </button>
        ) : null}
        <button
          type="button"
          data-testid={ids.pullUp}
          aria-label="Move the line up one item"
          className={step}
          disabled={!line.canMove.up}
          onClick={() => line.moveBy(-1)}
        >
          ▲
        </button>
        <button
          type="button"
          data-testid={ids.pullDown}
          aria-label="Move the line down one item"
          className={step}
          disabled={!line.canMove.down}
          onClick={() => line.moveBy(1)}
        >
          ▼
        </button>
        <button
          type="button"
          data-testid={ids.pullGo}
          className="btn-signal min-h-[var(--touch-target)]"
          disabled={line.count === 0}
          onClick={() => void line.pull()}
        >
          Pull
        </button>
      </div>
    </div>
  );
}
