import { useState, type FormEvent, type KeyboardEvent } from 'react';
import { createPortal } from 'react-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { ApiRequestError } from '@shiplean/core/api/client';
import { SaveBar } from '../../../touch/SaveBar';
import { useTouchShell } from '../../../touch/useTouchShell';
import { useSaveSprintGoal } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';

interface SprintGoalEditorProps {
  project: ProjectDto;
  sprint: SprintDto;
  /** Called after a successful save (the sprint list is already refreshed) and on Cancel or Escape. */
  onDone: () => void;
}

/**
 * The goal in place: Save patches only the goal, Cancel and Escape leave it as it was. At 900px and below (SHIP-724 D9)
 * it is a 16px textarea, and the SaveBar's Discard and Save stand in for Cancel and Save.
 */
export function SprintGoalEditor({ project, sprint, onDone }: SprintGoalEditorProps) {
  const { touch } = useTouchShell();
  const [goal, setGoal] = useState(sprint.goal);
  const save = useSaveSprintGoal(project.id, sprint.id);
  const submit = (event: FormEvent) => {
    event.preventDefault();
    save.mutate(goal, { onSuccess: onDone });
  };
  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') onDone();
  };
  const error = save.isError ? (
    <span data-testid={ids.goalError} role="alert" className="text-alert">
      {save.error instanceof ApiRequestError ? save.error.message : 'The goal could not be saved.'}
    </span>
  ) : null;

  if (touch) {
    return (
      <form className="mt-1 grid gap-1.5" onSubmit={submit} onKeyDown={onKeyDown}>
        <label
          htmlFor="sprint-goal-input"
          className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase"
        >
          Goal
        </label>
        <textarea
          id="sprint-goal-input"
          data-testid={ids.goalInput}
          className="field min-h-[76px] w-full resize-y py-2 leading-snug"
          value={goal}
          autoFocus
          onChange={(event) => setGoal(event.target.value)}
        />
        <span data-testid={ids.goalHint} className="text-[length:var(--text-xs)] text-ink-muted">
          Shown on the sprint&apos;s pages and to agents planning against it.
        </span>
        {error}
        {/* At the end of the page, so its spacer lets the page scroll clear of it rather than opening a gap here. */}
        {createPortal(
          <SaveBar
            dirty
            message={goal === sprint.goal ? 'No changes yet' : 'Goal not saved'}
            onDiscard={onDone}
            onSave={() => save.mutate(goal, { onSuccess: onDone })}
            saving={save.isPending}
          />,
          document.body,
        )}
      </form>
    );
  }
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
      {error}
    </form>
  );
}
