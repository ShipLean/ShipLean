import { backlogTestIds as ids } from '../testIds';

/** The not-estimated banner at touch (SHIP-1626 D5): the one check the phone design shows, with Estimate to work through it. */
export function NotEstimatedBanner({
  count,
  top,
  onEstimate,
}: {
  count: number;
  top: number;
  onEstimate: () => void;
}) {
  return (
    <div
      data-testid={ids.notEstimatedBanner}
      role="status"
      className="flex min-h-11 shrink-0 items-center gap-3 border-b border-edge border-l-[3px] border-l-caution bg-warn-soft px-3 py-1 text-[length:var(--text-sm)]"
    >
      <span className="min-w-0 flex-1">
        <b className="tabular-nums">{count} not estimated</b> in the top {top}.
      </span>
      <button
        type="button"
        data-testid={ids.notEstimatedEstimate}
        className="min-h-[var(--touch-target)] rounded-control border border-edge bg-panel px-3 font-medium"
        onClick={onEstimate}
      >
        Estimate
      </button>
    </div>
  );
}
