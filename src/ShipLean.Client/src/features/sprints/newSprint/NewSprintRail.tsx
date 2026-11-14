import { useState, type FormEvent } from 'react';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { SprintCreate } from '@shiplean/core/api/sprints';
import { Dialog } from '../../../components/Dialog';
import { useTouchShell } from '../../../touch/useTouchShell';
import { useDebouncedValue } from '../../workItem/form/description/useDebouncedValue';
import { useCreateSprint, useNextSprint, useSuggestedSprintName } from '../sprintsApi';
import { SprintForm, type SprintFormIds, type SprintFormValues } from '../form/SprintForm';
import { sprintsTestIds as ids } from '../testIds';

const NAME_PAUSE_MS = 250;

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
 * goes back to the defaults that now follow it. While the name is untouched, changing the dates swaps in the name the
 * server would give a sprint with them; once the person types a name it stays theirs. A refusal from the server (overlap, name taken) shows beside the button.
 */
export function NewSprintRail({
  projectId,
  onClose,
}: {
  projectId: string;
  /** Closes the full-screen Dialog the form opens in at 900px and below: ✕, and after the sprint is created. */
  onClose?: () => void;
}) {
  const { touch } = useTouchShell();
  const next = useNextSprint(projectId);
  const defaults = next.data;
  const create = useCreateSprint(projectId);
  // The form as the person has it once they touch it: it then stays put while the defaults refetch underneath.
  const [edited, setEdited] = useState<SprintCreate | null>(null);
  const [rangeError, setRangeError] = useState(false);
  const [nameTouched, setNameTouched] = useState(false);
  const typed = edited ?? defaults;
  const start = useDebouncedValue(typed?.startDate ?? '', NAME_PAUSE_MS);
  const end = useDebouncedValue(typed?.endDate ?? '', NAME_PAUSE_MS);
  const range = { start, end };
  const suggestion = useSuggestedSprintName(projectId, range, edited !== null && !nameTouched);
  // The name follows the dates, so until the suggestion for the typed range has arrived Create would send the old one.
  const nameStale =
    edited !== null &&
    !nameTouched &&
    (start !== typed?.startDate || end !== typed?.endDate || suggestion.isFetching);
  const form =
    typed && edited !== null && !nameTouched && suggestion.data
      ? { ...typed, name: suggestion.data.name }
      : typed;

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!form || create.isPending || nameStale) return;
    if (form.startDate === '' || form.endDate === '' || form.endDate < form.startDate) {
      setRangeError(true);
      return;
    }
    create.mutate(
      { ...form, name: form.name.trim() },
      {
        onSuccess: () => {
          setEdited(null);
          setNameTouched(false);
          onClose?.();
        },
      },
    );
  }

  const edit = (field: keyof SprintFormValues) => (value: string) => {
    if (!form) return;
    create.reset();
    setRangeError(false);
    if (field === 'name') setNameTouched(true);
    setEdited({ ...form, [field]: value });
  };

  const error = rangeError
    ? 'End must be on or after the start.'
    : create.error instanceof ApiRequestError
      ? create.error.message
      : create.error
        ? 'The sprint could not be created.'
        : '';

  const formBody =
    form === undefined ? (
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
        pending={create.isPending || nameStale}
        error={error}
      />
    );
  const note = (
    <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
      Defaults: the next name in the project's sprint naming, starting on the first working day
      after the last sprint, the same length as it. Overlapping dates are rejected.
    </p>
  );

  if (touch && onClose !== undefined) {
    // Create is the Dialog's top right action, so the form's own submit button (Enter still submits) is hidden.
    return (
      <Dialog
        title="New sprint"
        onClose={onClose}
        primaryAction={
          <button
            type="button"
            data-testid={ids.newSprintCreate}
            className="btn-signal h-[var(--touch-target)]"
            disabled={form === undefined || create.isPending || nameStale}
            onClick={(event) => submit(event)}
          >
            Create
          </button>
        }
      >
        <div data-testid={ids.newSprint} className="grid gap-2 [&_button[type=submit]]:hidden">
          {formBody}
          {note}
        </div>
      </Dialog>
    );
  }

  return (
    <aside
      data-testid={ids.newSprint}
      className="grid content-start gap-2 rounded-md border border-edge bg-panel p-4"
    >
      <div className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        New sprint
      </div>
      {formBody}
      {note}
    </aside>
  );
}
