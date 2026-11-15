import type { RetroItemDto } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../../testIds';

const base =
  'grid min-h-[52px] min-w-0 content-center justify-items-center rounded-control border border-edge bg-panel px-2 text-ink';

function Value({ value, of }: { value: number; of?: number }) {
  return (
    <span className="text-[20px] leading-tight font-bold tabular-nums">
      {value}
      {of === undefined ? null : (
        <small className="text-[length:var(--text-sm)] font-medium text-ink-muted"> / {of}</small>
      )}
    </span>
  );
}

/** The header's three numbers as one row of 52px buttons (R2): To decide toggles the filter, Action items opens its sheet, Decided is static. */
export function ItemKpiButtons({
  totals,
  toDecideOn,
  onToDecide,
  onActions,
}: {
  totals: RetroItemDto['totals'];
  toDecideOn: boolean;
  onToDecide: () => void;
  onActions: () => void;
}) {
  return (
    <div className="grid grid-cols-3 gap-2">
      <button
        type="button"
        data-testid={ids.retroItemKpiToDecide}
        aria-pressed={toDecideOn}
        className={`${base} cursor-pointer aria-pressed:border-signal aria-pressed:bg-signal-soft`}
        onClick={onToDecide}
      >
        <Value value={totals.toDecide} />
        <span className="text-[length:var(--text-xs)] text-ink-muted">To decide</span>
      </button>
      <button
        type="button"
        data-testid={ids.retroItemKpiActions}
        aria-haspopup="dialog"
        className={`${base} cursor-pointer`}
        onClick={onActions}
      >
        <Value value={totals.actions} />
        <span className="text-[length:var(--text-xs)] text-ink-muted">Action items ›</span>
      </button>
      <div data-testid={ids.retroItemKpiDecided} className={base}>
        <Value value={totals.decided} of={totals.decidable} />
        <span className="text-[length:var(--text-xs)] text-ink-muted">Decided</span>
      </div>
    </div>
  );
}
