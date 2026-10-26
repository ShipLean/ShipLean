import { useState, type FormEvent, type KeyboardEvent } from 'react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { ApiRequestError } from '../../../api/client';
import { useSaveSprintGoal } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';

interface SprintGoalEditorProps {
  project: ProjectDto;
  sprint: SprintDto;
  /** Called after a successful save (the sprint list is already refreshed) and on Cancel or Escape. */
  onDone: () => void;
}

/** The goal in place: Save patches only the goal, Cancel and Escape leave it as it was. */
export function SprintGoalEditor({ project, sprint, onDone }: SprintGoalEditorProps) {
  const [goal, setGoal] = useState(sprint.goal);
  const save = useSaveSprintGoal(project.id, sprint.id);
  const submit = (event: FormEvent) => {
    event.preventDefault();
    save.mutate(goal, { onSuccess: onDone });
  };
  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') onDone();
  };
  return (
    <form
      className="flex max-w-[760px] flex-wrap items-baseline gap-[var(--space-2)]"
      onSubmit={submit}
      onKeyDown={onKeyDown}
    >
      <label
        htmlFor="sprint-goal-input"
        className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase"
      >
        Goal
      </label>
      <input
        id="sprint-goal-input"
        data-testid={ids.goalInput}
        className="field min-w-64 flex-1"
        value={goal}
        autoFocus
        onChange={(event) => setGoal(event.target.value)}
      />
      <button
        type="submit"
        data-testid={ids.goalSave}
        disabled={save.isPending}
        className="inline-flex h-7 items-center rounded-control border border-signal bg-signal px-2.5 text-[length:var(--text-sm)] font-medium text-signal-ink"
      >
        Save
      </button>
      <button
        type="button"
        data-testid={ids.goalCancel}
        className="inline-flex h-7 items-center rounded-control border border-transparent bg-transparent px-2.5 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft"
        onClick={onDone}
      >
        Cancel
      </button>
      {save.isError ? (
        <span data-testid={ids.goalError} role="alert" className="text-alert">
          {save.error instanceof ApiRequestError
            ? save.error.message
            : 'The goal could not be saved.'}
        </span>
      ) : null}
    </form>
  );
}
