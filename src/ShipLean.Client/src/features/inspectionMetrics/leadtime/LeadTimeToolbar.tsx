import type {
  InspectionCoverageDto,
  InspectionCut,
  InspectionRange,
} from '@shiplean/core/api/views';
import { LOW_COVERAGE_PERCENT } from '../coverage';
import { inspectionMetricsTestIds as ids } from '../testIds';

export interface LeadTimeToolbarProps {
  range: InspectionRange;
  cut: InspectionCut;
  coverage: InspectionCoverageDto;
  /** "sprint" or "week". */
  unit: string;
  onRange(range: InspectionRange): void;
  onCut(cut: InspectionCut): void;
}

const RANGES = (unit: string): readonly (readonly [InspectionRange, string])[] => [
  ['last6', `Last 6 ${unit}s`],
  ['quarter', 'This quarter'],
  ['lastQuarter', 'Last quarter'],
  ['all', `All ${unit}s`],
];

const CUTS: readonly (readonly [InspectionCut, string])[] = [
  ['none', 'All tasks'],
  ['feature', 'By Feature'],
  ['who', 'By who'],
];

/** The range and split pickers, and how many tasks had a commit time. */
export function LeadTimeToolbar({
  range,
  cut,
  coverage,
  unit,
  onRange,
  onCut,
}: LeadTimeToolbarProps) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <select
        data-testid={ids.rangePicker}
        aria-label="Range"
        className="field w-auto"
        value={range}
        onChange={(event) => {
          const picked = RANGES(unit).find(([value]) => value === event.target.value);
          if (picked !== undefined) onRange(picked[0]);
        }}
      >
        {RANGES(unit).map(([value, label]) => (
          <option key={value} data-testid={ids.rangeOption} value={value}>
            {label}
          </option>
        ))}
      </select>
      <div
        data-testid={ids.cutPicker}
        role="group"
        aria-label="Split by"
        className="inline-flex overflow-hidden rounded-md border border-edge"
      >
        {CUTS.map(([value, label]) => (
          <button
            key={value}
            type="button"
            data-testid={ids.cutOption}
            data-value={value}
            aria-pressed={value === cut}
            onClick={() => onCut(value)}
            className={`px-2.5 py-1 text-[length:var(--text-sm)] ${
              value === cut ? 'bg-signal-soft font-semibold text-ink' : 'bg-panel text-ink-muted'
            }`}
          >
            {label}
          </button>
        ))}
      </div>
      {coverage.tasks > 0 && (
        <span
          data-testid={ids.coverageChip}
          className={`rounded-full px-2.5 py-0.5 text-[length:var(--text-xs)] font-semibold ${
            coverage.percent !== null && coverage.percent < LOW_COVERAGE_PERCENT ? 'text-caution' : 'text-ink'
          } bg-sunken`}
        >
          Commit times: {coverage.withCommitTime} of {coverage.tasks} tasks
        </span>
      )}
    </div>
  );
}
