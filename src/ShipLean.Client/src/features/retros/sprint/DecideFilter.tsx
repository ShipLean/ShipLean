import { retrosTestIds as ids } from '../testIds';

const OPTIONS = [
  { id: 'all', label: 'All points' },
  { id: 'open', label: 'To decide' },
] as const;

/** The Decision toggle: All points, or To decide (the points with no action item and no Not needed reason). */
export function DecideFilter({
  decide,
  onChange,
}: {
  decide: boolean;
  onChange: (value: 'all' | 'open') => void;
}) {
  return (
    <div
      role="group"
      aria-label="Decision"
      data-testid={ids.retroToDecideFilter}
      className="inline-flex overflow-hidden rounded-control border border-edge text-[length:var(--text-sm)]"
    >
      {OPTIONS.map((option) => (
        <button
          key={option.id}
          type="button"
          data-value={option.id}
          aria-pressed={decide === (option.id === 'open')}
          className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-ink last:border-r-0 aria-pressed:bg-canvas aria-pressed:font-semibold touch:min-h-[var(--touch-target)]"
          onClick={() => onChange(option.id)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
