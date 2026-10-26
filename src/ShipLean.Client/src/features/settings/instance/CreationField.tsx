import type { ProjectCreation } from '../../../api/projects';
import { settingsTestIds } from '../testIds';

const options: { value: ProjectCreation; label: string; testId: string }[] = [
  { value: 'everyone', label: 'Everyone', testId: settingsTestIds.instanceCreationEveryone },
  {
    value: 'admins',
    label: 'Instance admins only',
    testId: settingsTestIds.instanceCreationAdmins,
  },
];

const hint = (value: ProjectCreation) =>
  value === 'everyone'
    ? 'Anyone signed in sees + New project in the project picker and becomes the new project’s Admin.'
    : 'Only instance admins see + New project. Others are told who to ask.';

/** Who can create projects: a two-way choice that is part of the General draft. */
export function CreationField({
  value,
  disabled,
  onChange,
}: {
  value: ProjectCreation;
  disabled: boolean;
  onChange(value: ProjectCreation): void;
}) {
  return (
    <div className="grid gap-1 text-[length:var(--text-sm)]">
      <span className="font-medium text-ink">Who can create projects</span>
      <div role="group" aria-label="Who can create projects" className="flex gap-1">
        {options.map((o) => (
          <button
            key={o.value}
            type="button"
            data-testid={o.testId}
            aria-pressed={value === o.value}
            disabled={disabled}
            className={value === o.value ? 'btn-signal' : 'btn-ghost'}
            onClick={() => onChange(o.value)}
          >
            {o.label}
          </button>
        ))}
      </div>
      <span className="text-ink-muted">
        {hint(value)} Project keys are unique across the install either way.
      </span>
    </div>
  );
}
