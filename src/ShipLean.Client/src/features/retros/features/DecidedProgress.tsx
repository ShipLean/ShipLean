import type { RetroAggregateDto } from '../../../api/retros';

/** How many decidable points are decided, as a bar and "decided/decidable"; "nothing to decide" when there are none. */
export function DecidedProgress({ totals }: { totals: RetroAggregateDto }) {
  if (totals.decidable === 0) {
    return <span className="text-[length:var(--text-xs)] text-ink-muted">nothing to decide</span>;
  }
  return (
    <span
      title={`${totals.decided} of ${totals.decidable} points decided`}
      className="inline-flex items-center gap-1.5 text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted tabular-nums"
    >
      <i className="block h-1.5 w-11 overflow-hidden rounded-[3px] bg-sunken">
        <b
          className="block h-full bg-good"
          style={{ width: `${Math.round((totals.decided / totals.decidable) * 100)}%` }}
        />
      </i>
      {totals.decided}/{totals.decidable}
    </span>
  );
}
