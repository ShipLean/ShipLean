import { useEffect, useRef, useState, type ComponentType, type RefObject } from 'react';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import type { SprintGoalEditSlotProps } from '../../slots/SprintGoalEditSlot';
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
}) {
  const [editing, setEditing] = useState(false);
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
