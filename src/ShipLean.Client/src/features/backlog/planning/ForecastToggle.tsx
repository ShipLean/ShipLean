import { backlogTestIds as ids } from '../testIds';

// Planning's one option: show the forecast lines and the velocity override.
export function ForecastToggle({
  checked,
  onChange,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)] text-ink">
      <input
        type="checkbox"
        data-testid={ids.planningForecastToggle}
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      Forecast
    </label>
  );
}
