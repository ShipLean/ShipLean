import { taskboardTestIds as ids } from '../testIds';
import type { TaskboardGrouping } from '../taskboardApi';

const options: { id: TaskboardGrouping; label: string }[] = [
  { id: 'stories', label: 'Stories' },
  { id: 'people', label: 'People' },
];

/** Group by Stories or People. */
export function GroupBySwitch({
  value,
  onChange,
}: {
  value: TaskboardGrouping;
  onChange: (next: TaskboardGrouping) => void;
}) {
  return (
    <div
      data-testid={ids.groupBy}
      role="group"
      aria-label="Group by"
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          data-testid={ids.groupOption}
          data-option={option.id}
          aria-pressed={value === option.id}
          className={`px-3 text-[length:var(--text-sm)] ${
            value === option.id ? 'bg-signal-soft text-ink' : 'text-ink-muted'
          }`}
          style={{ height: 'var(--control-height)' }}
          onClick={() => onChange(option.id)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
