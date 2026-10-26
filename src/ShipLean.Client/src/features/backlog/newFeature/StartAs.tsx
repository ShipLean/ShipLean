import type { StateDto } from '../../../api/projects';
import { backlogTestIds as ids } from '../testIds';

// The states a new Feature can start in: the type's first Proposed state and its first In Progress state (Idea, Active).
export function startStatesOf(states: StateDto[]): StateDto[] {
  const features = states.filter((s) => s.type === 'feature').sort((a, b) => a.order - b.order);
  return (['proposed', 'inProgress'] as const).flatMap((category) => {
    const first = features.find((s) => s.category === category);
    return first ? [first] : [];
  });
}

// Start as: one button per start state, the first (Proposed) is the default.
export function StartAs({
  options,
  value,
  onChange,
}: {
  options: StateDto[];
  value: string;
  onChange: (stateId: string) => void;
}) {
  return (
    <div
      data-testid={ids.newFeatureStart}
      role="radiogroup"
      aria-label="Start as"
      className="inline-flex w-fit overflow-hidden rounded-control border border-edge"
    >
      {options.map((state) => (
        <button
          key={state.id}
          type="button"
          role="radio"
          aria-checked={state.id === value}
          data-testid={ids.newFeatureStartOption}
          data-state={state.name}
          className={`h-[var(--control-height)] px-3 text-[length:var(--text-sm)] ${
            state.id === value ? 'bg-signal-soft font-semibold text-ink' : 'text-ink-muted'
          }`}
          onClick={() => onChange(state.id)}
        >
          {state.name}
        </button>
      ))}
    </div>
  );
}
