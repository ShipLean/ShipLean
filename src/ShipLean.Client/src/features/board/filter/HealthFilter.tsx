import type { FeatureHealth } from '@shiplean/core/api/screens';
import { boardTestIds as ids } from '../testIds';
import { NONE } from './filterQuery';

const OPTIONS: { value: FeatureHealth | typeof NONE; label: string }[] = [
  { value: 'onTrack', label: 'On track' },
  { value: 'atRisk', label: 'At risk' },
  { value: 'blocked', label: 'Blocked' },
  { value: NONE, label: 'Not set' },
];

// Any Feature health, one of the three, or Features with none set (SHIP-907 H7). The Features board only.
export function HealthFilter({
  value,
  onChange,
}: {
  value: FeatureHealth | typeof NONE | undefined;
  onChange: (health: FeatureHealth | typeof NONE | undefined) => void;
}) {
  return (
    <select
      data-testid={ids.filterHealth}
      className="field"
      aria-label="Health"
      value={value ?? ''}
      onChange={(event) =>
        onChange(
          event.target.value === ''
            ? undefined
            : (event.target.value as FeatureHealth | typeof NONE),
        )
      }
    >
      <option value="">Any</option>
      {OPTIONS.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}
