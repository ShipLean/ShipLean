import { useState, type FormEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { SprintCreate } from '../../../api/sprints';
import { useCreateSprint, useNextSprint } from '../sprintsApi';
import { SprintForm, type SprintFormIds, type SprintFormValues } from '../form/SprintForm';
import { sprintsTestIds as ids } from '../testIds';

const NEW_SPRINT_IDS: SprintFormIds = {
  name: ids.newSprintName,
  start: ids.newSprintStart,
  end: ids.newSprintEnd,
  goal: ids.newSprintGoal,
  submit: ids.newSprintSubmit,
  error: ids.newSprintError,
};

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

  const edit = (field: keyof SprintFormValues) => (value: string) => {
    if (!form) return;
    create.reset();
    setRangeError(false);
    setEdited({ ...form, [field]: value });
  };

  const error = rangeError
    ? 'End must be on or after the start.'
    : create.error instanceof ApiRequestError
      ? create.error.message
      : create.error
        ? 'The sprint could not be created.'
        : '';

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
        <SprintForm
          idPrefix="new-sprint"
          ids={NEW_SPRINT_IDS}
          values={form}
          onChange={(field, value) => edit(field)(value)}
          onSubmit={submit}
          submitLabel="Create sprint"
          pending={create.isPending}
          error={error}
        />
      )}
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Defaults: the next number, starting on the first working day after the last sprint, the same
        length as it. Overlapping dates are rejected.
      </p>
    </aside>
  );
}
