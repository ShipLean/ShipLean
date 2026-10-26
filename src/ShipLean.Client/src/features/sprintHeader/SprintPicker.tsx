import { useId, useRef, useState, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { routePath } from '../../routes';
import type { SprintTab } from './SprintHeader';
import { sprintHeaderTestIds as ids } from './testIds';

const tabRoute = {
  backlog: 'sprintBacklog',
  taskboard: 'taskboard',
  capacity: 'capacity',
} as const satisfies Record<SprintTab, 'sprintBacklog' | 'taskboard' | 'capacity'>;

// `Sprint 24 ▾` opening a listbox. The arrows only move the highlight; the route changes when a sprint is picked.
export function SprintPicker({
  project,
  sprint,
  sprints,
  tab,
}: {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
  tab: SprintTab;
}) {
  const navigate = useNavigate();
  const listId = useId();
  const buttonRef = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false);
  const [highlight, setHighlight] = useState(0);
  const optionId = (sprintId: string) => `${listId}-${sprintId}`;

  function show() {
    setHighlight(
      Math.max(
        sprints.findIndex((s) => s.id === sprint.id),
        0,
      ),
    );
    setOpen(true);
  }

  function close() {
    setOpen(false);
    buttonRef.current?.focus();
  }

  function pick(picked: SprintDto) {
    close();
    if (picked.id !== sprint.id) {
      void navigate(routePath(tabRoute[tab], { key: project.key, sprintId: picked.id }));
    }
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setHighlight((highlight + step + sprints.length) % sprints.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      pick(sprints[highlight]);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      close();
    }
  }

  return (
    <div className="relative">
      <button
        ref={buttonRef}
        type="button"
        data-testid={ids.picker}
        aria-haspopup="listbox"
        aria-expanded={open}
        title="Switch sprint"
        className="inline-flex items-center gap-1.5 border-0 bg-transparent p-0 text-[length:var(--text-lg)] font-semibold"
        onClick={() => (open ? close() : show())}
      >
        {sprint.name} <span className="text-[length:var(--text-xs)] text-ink-muted">▾</span>
      </button>
      {open ? (
        <ul
          ref={(node) => node?.focus()}
          id={listId}
          role="listbox"
          tabIndex={-1}
          aria-label="Sprints"
          aria-activedescendant={optionId(sprints[highlight].id)}
          data-testid={ids.pickerList}
          className="absolute top-full left-0 z-50 m-0 mt-1 max-h-80 min-w-[180px] list-none overflow-auto rounded-panel border border-edge bg-panel p-1 shadow-[0_12px_32px_rgb(0_0_0/0.18)] focus:outline-0"
          onKeyDown={onKeyDown}
          onBlur={() => setOpen(false)}
        >
          {sprints.map((s, index) => (
            <li
              key={s.id}
              id={optionId(s.id)}
              role="option"
              aria-selected={s.id === sprint.id}
              data-testid={ids.pickerOption}
              className={`cursor-pointer rounded-control px-2 py-1 text-[length:var(--text-sm)] ${
                index === highlight ? 'bg-signal-soft' : ''
              }`}
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => pick(s)}
            >
              {s.name}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
