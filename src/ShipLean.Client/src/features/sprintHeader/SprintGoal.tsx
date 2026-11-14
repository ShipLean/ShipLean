import { useEffect, useRef, useState, type ComponentType, type RefObject } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import type { SprintGoalEditSlotProps } from '../../slots/SprintGoalEditSlot';
import { useTouchShell } from '../../touch/useTouchShell';
import { sprintsTestIds } from '../sprints/testIds';
import { sprintHeaderTestIds as ids } from './testIds';

export function SprintGoal({
  project,
  sprint,
  sprints,
  Editor,
  onEditSprint,
  onEditGoal,
  editSprintRef,
  editing: editingFromOutside,
  onEditingChange,
}: {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
  // The goal editor mounted in place while editing (the sprintGoalEdit slot).
  Editor: ComponentType<SprintGoalEditSlotProps>;
  // Opens the Edit sprint rail (name, dates, goal); the button is left out when not given.
  onEditSprint?: () => void;
  // Called when the goal editor opens, so an open Edit sprint rail can close.
  onEditGoal?: () => void;
  // The Edit sprint button, so the rail can return focus to it.
  editSprintRef?: RefObject<HTMLButtonElement | null>;
  // Given, the editor opens and closes from outside (the touch header's ⋯ opens it); left out, the Edit button does.
  editing?: boolean;
  onEditingChange?: (editing: boolean) => void;
}) {
  const { touch } = useTouchShell();
  const [editingHere, setEditingHere] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const editing = editingFromOutside ?? editingHere;
  const setEditing = (next: boolean) => {
    if (editingFromOutside === undefined) setEditingHere(next);
    onEditingChange?.(next);
  };
  const editRef = useRef<HTMLButtonElement>(null);
  const returnFocus = useRef(false);

  useEffect(() => {
    if (!editing && returnFocus.current) {
      returnFocus.current = false;
      editRef.current?.focus();
    }
  }, [editing]);

  if (editing) {
    return (
      <Editor
        project={project}
        sprint={sprint}
        sprints={sprints}
        onDone={() => {
          returnFocus.current = true;
          setEditing(false);
        }}
      />
    );
  }
  if (touch) {
    // Two lines at most; a tap shows the rest (SHIP-724). Edit goal and Edit sprint are in the header's ⋯.
    return (
      <button
        type="button"
        data-testid={ids.goalToggle}
        aria-expanded={expanded}
        className="mt-1 grid min-h-[var(--touch-target)] grid-cols-[auto_minmax(0,1fr)] items-baseline gap-2 border-0 bg-transparent p-0 text-left text-[15px] leading-snug text-ink"
        onClick={() => setExpanded(!expanded)}
      >
        <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
          Goal
        </span>
        <span
          data-testid={ids.goal}
          className={`${expanded ? '' : 'line-clamp-2'} ${sprint.goal === '' ? 'text-ink-muted italic' : ''}`}
        >
          {sprint.goal === '' ? 'No goal set' : sprint.goal}
        </span>
      </button>
    );
  }
  return (
    <div className="flex max-w-[760px] items-baseline gap-[var(--space-2)]">
      <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Goal
      </span>
      <span data-testid={ids.goal}>{sprint.goal === '' ? 'No goal set' : sprint.goal}</span>
      <button
        ref={editRef}
        type="button"
        data-testid={ids.goalEdit}
        aria-label="Edit goal"
        className="inline-flex h-7 items-center rounded-control border border-transparent bg-transparent px-2.5 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal"
        onClick={() => {
          onEditGoal?.();
          setEditing(true);
        }}
      >
        Edit
      </button>
      {onEditSprint === undefined ? null : (
        <button
          ref={editSprintRef}
          type="button"
          data-testid={sprintsTestIds.editSprintOpen}
          className="inline-flex h-7 items-center rounded-control border border-transparent bg-transparent px-2.5 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal"
          onClick={onEditSprint}
        >
          Edit sprint
        </button>
      )}
    </div>
  );
}
