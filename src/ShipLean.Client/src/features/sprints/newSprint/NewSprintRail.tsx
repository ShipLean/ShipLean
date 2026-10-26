import { useState, type FormEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { SprintCreate } from '../../../api/sprints';
import { useCreateSprint, useNextSprint } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';

/**
 * The New sprint form. It starts from the server's defaults (the next number, the first working day after the last
 * sprint, the last sprint's length); what the person types replaces them until the sprint is created, when the form
 * goes back to the defaults that now follow it. A refusal from the server (overlap, name taken) shows beside the button.
 */
export function NewSprintRail({ projectId }: { projectId: string }) {
  const next = useNextSprint(projectId);
  const defaults = next.data;
  const create = useCreateSprint(projectId);
  // The form as the person has it once they touch it: it then stays put while the defaults refetch underneath.
  const [edited, setEdited] = useState<SprintCreate | null>(null);
  const [rangeError, setRangeError] = useState(false);
  const form = edited ?? defaults;

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!form || create.isPending) return;
    if (form.startDate === '' || form.endDate === '' || form.endDate < form.startDate) {
      setRangeError(true);
      return;
    }
    create.mutate({ ...form, name: form.name.trim() }, { onSuccess: () => setEdited(null) });
  }

  const edit = (field: keyof SprintCreate) => (value: string) => {
    if (!form) return;
    create.reset();
    setRangeError(false);
    setEdited({ ...form, [field]: value });
  };

  return (
    <aside
      data-testid={ids.newSprint}
      className="grid content-start gap-2 rounded-md border border-edge bg-panel p-4"
    >
      <div className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        New sprint
      </div>
      {form === undefined ? (
        next.isError ? (
          <p data-testid={ids.newSprintLoadError} role="alert" className="m-0 text-alert">
            The defaults could not be loaded.
          </p>
        ) : (
          <p className="m-0 text-ink-muted">Loading…</p>
        )
      ) : (
        <form
          className="grid grid-cols-[auto_minmax(0,1fr)] items-center gap-x-3 gap-y-2"
          onSubmit={submit}
        >
          <label htmlFor="new-sprint-name" className="text-[length:var(--text-sm)] text-ink-muted">
            Name
          </label>
          <input
            id="new-sprint-name"
            data-testid={ids.newSprintName}
            className="field w-full"
            value={form.name}
            onChange={(event) => edit('name')(event.target.value)}
          />
          <label htmlFor="new-sprint-start" className="text-[length:var(--text-sm)] text-ink-muted">
            Start
          </label>
          <input
            id="new-sprint-start"
            data-testid={ids.newSprintStart}
            type="date"
            className="field w-full"
            value={form.startDate}
            onChange={(event) => edit('startDate')(event.target.value)}
          />
          <label htmlFor="new-sprint-end" className="text-[length:var(--text-sm)] text-ink-muted">
            End
          </label>
          <input
            id="new-sprint-end"
            data-testid={ids.newSprintEnd}
            type="date"
            className="field w-full"
            value={form.endDate}
            onChange={(event) => edit('endDate')(event.target.value)}
          />
          <label htmlFor="new-sprint-goal" className="text-[length:var(--text-sm)] text-ink-muted">
            Goal
          </label>
          <textarea
            id="new-sprint-goal"
            data-testid={ids.newSprintGoal}
            rows={2}
            className="field w-full"
            placeholder="Optional"
            value={form.goal}
            onChange={(event) => edit('goal')(event.target.value)}
          />
          <span />
          <span
            data-testid={ids.newSprintError}
            role="alert"
            className="text-[length:var(--text-sm)] text-alert"
          >
            {rangeError
              ? 'End must be on or after the start.'
              : create.error instanceof ApiRequestError
                ? create.error.message
                : create.error
                  ? 'The sprint could not be created.'
                  : ''}
          </span>
          <span />
          <div className="flex justify-end">
            <button
              type="submit"
              data-testid={ids.newSprintSubmit}
              className="btn-signal"
              disabled={create.isPending}
            >
              Create sprint
            </button>
          </div>
        </form>
      )}
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Defaults: the next number, starting on the first working day after the last sprint, the same
        length as it. Overlapping dates are rejected.
      </p>
    </aside>
  );
}
