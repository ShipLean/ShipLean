import type { FeatureHealth } from '@shiplean/core/api/screens';
import { featureTestIds as ids } from '../testIds';

const options: readonly { value: FeatureHealth | ''; label: string }[] = [
  { value: 'onTrack', label: 'On track' },
  { value: 'atRisk', label: 'At risk' },
  { value: 'blocked', label: 'Blocked' },
  { value: '', label: 'No health' },
];

// Chooses the Feature's health into the form's draft; the form's Save sends it.
export function HealthSelect({
  health,
  onChange,
}: {
  health: FeatureHealth | null;
  onChange: (health: FeatureHealth | null) => void;
}) {
  return (
    <select
      data-testid={ids.statusHealthSelect}
      aria-label="Health"
      value={health ?? ''}
      className="h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink focus-visible:outline-2 focus-visible:outline-signal"
      onChange={(event) => {
        const value = event.target.value;
        onChange(value === '' ? null : (value as FeatureHealth));
      }}
    >
      {options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}
