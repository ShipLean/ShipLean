import { componentTestIds } from './testIds';

export type StateCategory = 'proposed' | 'inProgress' | 'resolved' | 'completed' | 'removed';

const colours: Record<StateCategory, string> = {
  proposed: 'var(--state-proposed)',
  inProgress: 'var(--state-in-progress)',
  resolved: 'var(--state-resolved)',
  completed: 'var(--state-completed)',
  removed: 'var(--state-removed)',
};

/** A state's colour dot, by category (states are custom per project, so colour never follows the name). */
export function StateDot({ category, name }: { category: StateCategory; name?: string }) {
  return (
    <span
      data-testid={componentTestIds.stateDot}
      data-category={category}
      className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)] whitespace-nowrap"
    >
      <span
        aria-hidden="true"
        className="size-[7px] flex-none rounded-full"
        style={{ background: colours[category] }}
      />
      {name}
    </span>
  );
}
