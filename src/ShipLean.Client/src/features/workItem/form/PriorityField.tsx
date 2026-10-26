import { workItemTestIds as ids } from '../testIds';
import { FieldCell } from './FieldCell';

const priorities = [1, 2, 3, 4] as const;

export function PriorityField({
  value,
  changed,
  onChange,
}: {
  value: number;
  changed: boolean;
  onChange: (priority: number) => void;
}) {
  return (
    <FieldCell label="Priority" field="priority" changed={changed}>
      <div
        data-testid={ids.priority}
        role="group"
        aria-label="Priority, 1 is highest"
        className="inline-flex h-7 overflow-hidden rounded-control border border-edge"
      >
        {priorities.map((priority) => (
          <button
            key={priority}
            type="button"
            data-priority={priority}
            aria-pressed={value === priority}
            className={`w-7 text-[length:var(--text-sm)] ${
              value === priority
                ? 'bg-signal font-semibold text-signal-ink'
                : 'bg-panel text-ink hover:bg-signal-soft'
            }`}
            onClick={() => onChange(priority)}
          >
            {priority}
          </button>
        ))}
      </div>
    </FieldCell>
  );
}
