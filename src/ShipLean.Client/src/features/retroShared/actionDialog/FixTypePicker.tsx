import type { RetroFixType } from '../../../api/retros';
import { retroSharedTestIds as ids } from '../testIds';
import { retroFixOptions } from './retroFixTypes';

/** Type of fix: six cards, one checked. Required for a new action item. */
export function FixTypePicker({
  value,
  onChange,
}: {
  value: RetroFixType | null;
  onChange: (fix: RetroFixType) => void;
}) {
  return (
    <div className="grid gap-1">
      <span className="text-[length:var(--text-xs)] font-semibold text-ink-muted">Type of fix</span>
      <div
        role="radiogroup"
        aria-label="Type of fix"
        data-testid={ids.retroActionFix}
        className="grid grid-cols-3 gap-1.5 max-[620px]:grid-cols-2"
      >
        {retroFixOptions.map((fix) => (
          <label
            key={fix.id}
            data-testid={ids.retroActionFixOption}
            className={`grid cursor-pointer gap-0.5 rounded-control border px-2 py-1.5 text-[length:var(--text-sm)] font-medium text-ink ${value === fix.id ? 'border-signal bg-signal-soft' : 'border-edge'}`}
          >
            <input
              type="radio"
              name="retro-fix"
              value={fix.id}
              checked={value === fix.id}
              className="sr-only"
              onChange={() => onChange(fix.id)}
            />
            {fix.label}
            <span className="text-[length:var(--text-2xs)] leading-snug font-normal text-ink-muted">
              {fix.hint}
            </span>
          </label>
        ))}
      </div>
    </div>
  );
}
