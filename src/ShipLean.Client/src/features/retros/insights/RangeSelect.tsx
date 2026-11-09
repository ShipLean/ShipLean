import type { RetroRange } from '@shiplean/core/api/retrosApi';
import { retrosTestIds as ids } from '../testIds';

const SPRINT_RANGES = [
  { id: '6', label: 'Last 6 sprints' },
  { id: '3', label: 'Last 3 sprints' },
  { id: 'all', label: 'All sprints' },
] as const satisfies readonly { id: RetroRange; label: string }[];

const WEEK_RANGES = [
  { id: 'w6', label: 'Last 6 weeks' },
  { id: 'w12', label: 'Last 12 weeks' },
] as const satisfies readonly { id: RetroRange; label: string }[];

const ALL_RANGES: readonly { id: RetroRange; label: string }[] = [...SPRINT_RANGES, ...WEEK_RANGES];

/** The range as the address spells it; anything it doesn't know is the default, the last 6 sprints. */
export const rangeOf = (value: string | null): RetroRange =>
  ALL_RANGES.find((r) => r.id === value)?.id ?? '6';

/** The range select: by sprint or by week. */
export function RangeSelect({
  range,
  onChange,
}: {
  range: RetroRange;
  onChange: (range: RetroRange) => void;
}) {
  return (
    <select
      data-testid={ids.retroInsightsRange}
      aria-label="Range"
      value={range}
      className="rounded-control border border-edge bg-panel px-2 py-1 text-[length:var(--text-sm)]"
      onChange={(event) => onChange(rangeOf(event.target.value))}
    >
      <optgroup label="By sprint">
        {SPRINT_RANGES.map((r) => (
          <option key={r.id} value={r.id}>
            {r.label}
          </option>
        ))}
      </optgroup>
      <optgroup label="By week">
        {WEEK_RANGES.map((r) => (
          <option key={r.id} value={r.id}>
            {r.label}
          </option>
        ))}
      </optgroup>
    </select>
  );
}
