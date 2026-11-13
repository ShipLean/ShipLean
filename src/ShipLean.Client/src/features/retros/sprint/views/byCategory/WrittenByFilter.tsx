import { retrosTestIds as ids } from '../../../testIds';
import { WRITTEN_BY_OPTIONS, type WrittenBy } from './writtenBy';

/** The Written by toggle: All, Agents or People. */
export function WrittenByFilter({
  value,
  onChange,
}: {
  value: WrittenBy;
  onChange: (value: WrittenBy) => void;
}) {
  return (
    <div
      role="group"
      aria-label="Written by"
      data-testid={ids.retroWrittenByFilter}
      className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink-muted"
    >
      <span>Written by</span>
      <div className="inline-flex overflow-hidden rounded-control border border-edge">
        {WRITTEN_BY_OPTIONS.map((option) => (
          <button
            key={option.id}
            type="button"
            data-value={option.id}
            aria-pressed={value === option.id}
            className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-ink last:border-r-0 aria-pressed:bg-canvas aria-pressed:font-semibold touch:min-h-[var(--touch-target)]"
            onClick={() => onChange(option.id)}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  );
}
