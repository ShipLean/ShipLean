import { inspectionMetricsTestIds as ids } from '../testIds';
import { SPLIT_COLOURS } from './SplitBar';

// How one task's lead time is measured, drawn on a 0–100 track (design inspection.html ruleStrip): start → blocked → first
// resolve → rework after a review → last commit, then the part that isn't counted (last commit → Resolved).
const HATCH =
  'repeating-linear-gradient(135deg, color-mix(in srgb, var(--color-muted) 55%, transparent) 0 2px, transparent 2px 4px)';

const SEGMENTS = [
  { from: 0, to: 20, fill: SPLIT_COLOURS.active, label: 'Active' },
  { from: 20, to: 34, fill: SPLIT_COLOURS.blocked, label: 'Blocked' },
  { from: 34, to: 44, fill: SPLIT_COLOURS.active, label: 'Active' },
  {
    from: 44,
    to: 70,
    fill: SPLIT_COLOURS.rework,
    label: 'Rework: after a review asked for changes',
  },
  { from: 70, to: 78, fill: SPLIT_COLOURS.active, label: '' },
  { from: 78, to: 96, fill: HATCH, label: 'Not counted' },
] as const;

const TICKS = [
  { at: 0, label: 'Start: first Active or pick_up', align: 'start', strong: true },
  { at: 44, label: 'First Resolved', align: 'middle', strong: false },
  { at: 78, label: 'End: last commit (committed_at)', align: 'end', strong: true },
  { at: 96, label: 'Resolved', align: 'middle', strong: false },
] as const;

const shift = { start: '0%', middle: '-50%', end: '-100%' } as const;

export function LeadTimeRule() {
  return (
    <details
      data-testid={ids.leadTimeRule}
      open
      className="rounded-md border border-edge bg-panel [&[open]>summary]:border-b [&[open]>summary]:border-edge"
    >
      <summary className="flex cursor-pointer items-baseline gap-2 px-3 py-2">
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">
          How a task&apos;s lead time is measured
        </h3>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          the same rule for every number on this page
        </span>
      </summary>
      <div className="px-4 pt-3 pb-3">
        <div
          role="img"
          aria-label="Lead time runs from the first move to Active to the last commit; time after the last commit is not counted"
          className="relative h-[74px] text-[length:var(--text-xs)] text-ink-muted"
        >
          {SEGMENTS.map((seg) => (
            <span key={seg.from}>
              <i
                className="absolute top-[22px] block h-3 rounded-sm"
                style={{
                  left: `${seg.from}%`,
                  width: `calc(${seg.to - seg.from}% - 1px)`,
                  background: seg.fill,
                }}
              />
              {seg.label !== '' && (
                <span
                  className="absolute top-[38px] -translate-x-1/2 whitespace-nowrap"
                  style={{ left: `${(seg.from + seg.to) / 2}%` }}
                >
                  {seg.label}
                </span>
              )}
            </span>
          ))}
          {TICKS.map((tick) => (
            <span key={tick.at}>
              <i
                className="absolute top-4 block h-5 bg-ink"
                style={{ left: `${tick.at}%`, width: tick.strong ? 2 : 1 }}
              />
              <span
                className={`absolute top-0 whitespace-nowrap ${tick.strong ? 'font-semibold text-ink' : ''}`}
                style={{ left: `${tick.at}%`, transform: `translateX(${shift[tick.align]})` }}
              >
                {tick.label}
              </span>
            </span>
          ))}
          <i className="absolute top-[62px] left-0 block h-px w-[78%] bg-ink" />
          <i className="absolute top-[57px] left-0 block h-2.5 w-px bg-ink" />
          <i className="absolute top-[57px] left-[78%] block h-2.5 w-px bg-ink" />
          <span className="absolute top-[54px] left-[39%] -translate-x-1/2 bg-panel px-1 font-semibold text-ink">
            lead time
          </span>
        </div>
        <p className="m-0 mt-1 text-[length:var(--text-xs)] text-ink-muted">
          No commit time reported → the end is the first move to Resolved, and the task says{' '}
          <span className="font-semibold text-caution">to Resolved</span>. A task with no start is
          left out.
        </p>
      </div>
    </details>
  );
}
