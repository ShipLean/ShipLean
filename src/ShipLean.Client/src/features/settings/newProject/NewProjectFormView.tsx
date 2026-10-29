import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiRequestError } from '../../../api/client';
import type { MeDto } from '../../../api/me';
import { routePath } from '../../../routes';
import { useDebouncedValue } from '../../workItem/form/description/useDebouncedValue';
import { settingsTestIds as ids } from '../testIds';
import { MembersPanel } from './MembersPanel';
import {
  blocker,
  emptyForm,
  keyIsWellFormed,
  keyOf,
  toCreate,
  withSprintDefaults,
  type NewProjectForm,
} from './model';
import {
  TYPING_PAUSE_MS,
  useCopySources,
  useCreateProject,
  useFirstSprint,
  useKeyCheck,
} from './newProjectApi';
import { ProjectPanel } from './ProjectPanel';
import { SchedulePanel } from './SchedulePanel';
import { StartFromPanel } from './StartFromPanel';

export function NewProjectFormView({ me }: { me: MeDto }) {
  const navigate = useNavigate();
  const [typed, setForm] = useState<NewProjectForm>(emptyForm);
  const start = useDebouncedValue(typed.start, TYPING_PAUSE_MS);
  const end = useDebouncedValue(typed.end, TYPING_PAUSE_MS);
  // Once the dates are typed (and make a range) the suggested name follows them.
  const range =
    typed.datesEdited && start !== '' && end !== '' && end >= start ? { start, end } : undefined;
  const firstSprint = useFirstSprint(typed.workingDays, typed.sprint, range);
  const form = withSprintDefaults(typed, firstSprint.data);
  const patch = (change: Partial<NewProjectForm>) => setForm((f) => ({ ...f, ...change }));
  const sources = useCopySources().data ?? [];
  const name = form.name.trim();
  // A suggestion that is taken is skipped for its first alternative, until the person types a key.
  const suggested = useKeyCheck(keyOf(form), name);
  const skipTo =
    !form.keyEdited && suggested.check?.available === false
      ? suggested.check.alternatives[0]
      : undefined;
  const key = skipTo ?? keyOf(form);
  const { check, pending } = useKeyCheck(key, name);
  const create = useCreateProject((project) =>
    navigate(routePath('storiesBacklog', { key: project.key })),
  );

  const available = keyIsWellFormed(key) && check?.available === true;
  const hint = blocker(form, available, firstSprint.data?.name);
  const failure = create.error instanceof ApiRequestError ? create.error.message : undefined;

  return (
    <div data-testid={ids.newProjectForm} className="mx-auto grid max-w-3xl gap-2">
      <div className="flex items-center gap-3">
        <h1 className="text-[length:var(--text-lg)] font-semibold text-ink">New project</h1>
        <span
          className="ml-auto text-[length:var(--text-sm)] text-ink-muted"
          data-testid={ids.newProjectHint}
        >
          {hint ?? ''}
        </span>
        <Link
          to="/"
          data-testid={ids.newProjectCancel}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          Cancel
        </Link>
        <button
          type="button"
          data-testid={ids.newProjectCreate}
          disabled={hint !== null || create.isPending}
          className="h-8 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-medium text-signal-ink disabled:opacity-50"
          onClick={() => create.mutate(toCreate(form, firstSprint.data?.name))}
        >
          {available ? `Create ${key}` : 'Create project'}
        </button>
      </div>
      {failure === undefined ? null : (
        <div
          role="alert"
          data-testid={ids.newProjectError}
          className="text-[length:var(--text-sm)] text-alert"
        >
          {failure}
        </div>
      )}
      <ProjectPanel
        form={form}
        patch={patch}
        check={check}
        pending={pending}
        sources={sources}
        shownKey={key}
        skippedFrom={skipTo === undefined ? undefined : keyOf(form)}
      />
      <StartFromPanel form={form} patch={patch} sources={sources} />
      <MembersPanel form={form} patch={patch} myEmail={me.email} />
      <SchedulePanel form={form} patch={patch} />
    </div>
  );
}
