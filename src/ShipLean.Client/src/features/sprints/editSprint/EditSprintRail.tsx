import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { SprintDto, SprintUpdate } from '../../../api/sprints';
import { SprintForm, type SprintFormIds, type SprintFormValues } from '../form/SprintForm';
import { useUpdateSprint } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';

const EDIT_SPRINT_IDS: SprintFormIds = {
  name: ids.editSprintName,
  start: ids.editSprintStart,
  end: ids.editSprintEnd,
  goal: ids.editSprintGoal,
  submit: ids.editSprintSubmit,
  error: ids.editSprintError,
  cancel: ids.editSprintCancel,
  datesHint: ids.editSprintDatesHint,
};

interface EditSprintRailProps {
  projectId: string;
  sprint: SprintDto;
  /** Called after a successful save (the sprint list is already refreshed) and on Cancel. */
  onDone: () => void;
}

/**
 * The Edit sprint form: the sprint's name, dates and goal, prefilled. An ended sprint's dates are shown but locked (the
 * server refuses them). It sends only what changed, so a name edit never touches the dates. A refusal from the server
 * (overlap, name taken) shows beside Save. Mount it with `key={sprint.id}` so a different sprint starts clean.
 */
export function EditSprintRail({ projectId, sprint, onDone }: EditSprintRailProps) {
  const update = useUpdateSprint(projectId, sprint.id);
  // Only what the person has typed: the rest follows the sprint, so a refetch under the open form is not lost.
  const [edited, setEdited] = useState<Partial<SprintFormValues>>({});
  // What the person started from. A key typed back to it is not a change, so a teammate's edit to it is not overwritten.
  const [base] = useState<SprintFormValues>(() => ({
    name: sprint.name,
    startDate: sprint.startDate,
    endDate: sprint.endDate,
    goal: sprint.goal,
  }));
  const nameRef = useRef<HTMLInputElement>(null);
  useEffect(() => nameRef.current?.focus(), []);
  const [rangeError, setRangeError] = useState(false);
  const ended = sprint.status === 'past';
  const form: SprintFormValues = {
    name: sprint.name,
    startDate: sprint.startDate,
    endDate: sprint.endDate,
    goal: sprint.goal,
    ...edited,
  };

  function submit(event: FormEvent) {
    event.preventDefault();
    if (update.isPending) return;
    if (form.startDate === '' || form.endDate === '' || form.endDate < form.startDate) {
      setRangeError(true);
      return;
    }
    const name = form.name.trim();
    const changed = (field: keyof SprintFormValues) =>
      edited[field] !== undefined && form[field] !== base[field];
    const changes: SprintUpdate = {
      name: changed('name') && name !== base.name ? name : null,
      startDate: changed('startDate') ? form.startDate : null,
      endDate: changed('endDate') ? form.endDate : null,
      goal: changed('goal') ? form.goal : null,
    };
    if (Object.values(changes).every((value) => value === null)) {
      onDone();
      return;
    }
    update.mutate(changes, { onSuccess: onDone });
  }

  const change = (field: keyof SprintFormValues, value: string) => {
    update.reset();
    setRangeError(false);
    setEdited({ ...edited, [field]: value });
  };

  const error = rangeError
    ? 'End must be on or after the start.'
    : update.error instanceof ApiRequestError
      ? update.error.message
      : update.error
        ? 'The sprint could not be saved.'
        : '';

  return (
    <aside
      data-testid={ids.editSprint}
      className="grid content-start gap-2 rounded-md border border-edge bg-panel p-4"
    >
      <div className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Edit sprint
      </div>
      <SprintForm
        idPrefix={`edit-sprint-${sprint.id}`}
        ids={EDIT_SPRINT_IDS}
        values={form}
        nameRef={nameRef}
        onChange={change}
        onSubmit={submit}
        submitLabel="Save"
        pending={update.isPending}
        error={error}
        datesLocked={ended}
        datesHint="This sprint has ended, so its dates can't change. Its name and goal can."
        onCancel={onDone}
      />
    </aside>
  );
}
