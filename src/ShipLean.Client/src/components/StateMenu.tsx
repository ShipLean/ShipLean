import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { StateDot, type StateCategory } from './StateDot';
import { componentTestIds } from './testIds';

export interface StateOption {
  id: string;
  name: string;
  category: StateCategory;
}

const categoryLabels: Record<StateCategory, string> = {
  proposed: 'Proposed',
  inProgress: 'In progress',
  resolved: 'Resolved',
  completed: 'Completed',
  removed: 'Removed',
};

/** A list of a type's states. It takes focus when it mounts. Arrow keys move the highlight, Enter picks it, a click picks any; Esc and Tab out call `onClose`. */
export function StateMenu({
  states,
  currentId,
  onSelect,
  onClose,
}: {
  states: StateOption[];
  currentId?: string;
  onSelect: (stateId: string) => void;
  onClose: () => void;
}) {
  const list = useRef<HTMLUListElement>(null);
  const prefix = useId();
  useEffect(() => list.current?.focus(), []);
  const [active, setActive] = useState(() =>
    Math.max(
      0,
      states.findIndex((s) => s.id === currentId),
    ),
  );
  const last = states.length - 1;

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
    } else if (event.key === 'Tab') {
      onClose();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActive((i) => Math.min(last, i + 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((i) => Math.max(0, i - 1));
    } else if (event.key === 'Home') {
      event.preventDefault();
      setActive(0);
    } else if (event.key === 'End') {
      event.preventDefault();
      setActive(last);
    } else if (event.key === 'Enter' || event.key === ' ') {
      const state = states[active];
      if (state) {
        event.preventDefault();
        onSelect(state.id);
      }
    }
  };

  return (
    <ul
      ref={list}
      data-testid={componentTestIds.stateMenu}
      role="listbox"
      aria-label="State"
      tabIndex={0}
      aria-activedescendant={states[active] ? `${prefix}-${states[active].id}` : undefined}
      onKeyDown={onKeyDown}
      className="grid min-w-44 gap-px rounded-panel border border-edge bg-panel p-1 shadow-panel focus-visible:outline-2 focus-visible:outline-signal"
    >
      {states.map((state, index) => (
        <li
          key={state.id}
          id={`${prefix}-${state.id}`}
          data-testid={componentTestIds.stateMenuOption}
          data-category={state.category}
          data-active={index === active}
          role="option"
          aria-selected={state.id === currentId}
          className={`flex h-[30px] cursor-pointer items-center justify-between gap-3 rounded-control px-2 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft ${
            index === active ? 'bg-signal-soft' : ''
          } ${state.id === currentId ? 'font-semibold' : ''}`}
          onClick={() => onSelect(state.id)}
        >
          <StateDot category={state.category} name={state.name} />
          <span className="text-[length:var(--text-xs)] text-ink-muted">
            {categoryLabels[state.category]}
          </span>
        </li>
      ))}
    </ul>
  );
}
