import type { RetroRange } from '@shiplean/core/api/retrosApi';
import { actionSheetRowClass } from '../../../../touch/ActionSheet';
import { Sheet } from '../../../../touch/Sheet';
import { retrosTestIds as ids } from '../../testIds';
import { RANGE_GROUPS } from '../RangeSelect';

/** The Range sheet (D3): By sprint and By week; picking one applies it and closes the sheet. */
export function RangeSheet({
  range,
  onPick,
  onClose,
}: {
  range: RetroRange;
  onPick: (range: RetroRange) => void;
  onClose: () => void;
}) {
  return (
    <Sheet open title="Range" onClose={onClose}>
      {RANGE_GROUPS.map((group) => (
        <div
          key={group.label}
          data-testid={ids.retroInsightsRangeGroup}
          role="group"
          aria-label={group.label}
          className="grid gap-1"
        >
          <div className="pt-2.5 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
            {group.label}
          </div>
          {group.ranges.map((r) => (
            <button
              key={r.id}
              type="button"
              role="menuitemradio"
              aria-checked={r.id === range}
              data-testid={ids.retroInsightsRangeOption}
              data-value={r.id}
              className={`${actionSheetRowClass} gap-2 text-ink aria-checked:bg-signal-soft`}
              onClick={() => onPick(r.id)}
            >
              <span aria-hidden="true" className="w-4 flex-none font-bold text-signal">
                {r.id === range ? '✓' : ''}
              </span>
              {r.label}
            </button>
          ))}
        </div>
      ))}
      <p className="m-0 pt-2 text-[length:var(--text-sm)] text-ink-muted">
        By week counts each point in the week it was logged, so retros with no sprint land in a week
        too. Recurring and the verdicts follow the range you pick.
      </p>
    </Sheet>
  );
}
