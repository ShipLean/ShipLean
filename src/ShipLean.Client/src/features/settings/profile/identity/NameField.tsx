import { settingsTestIds } from '../../testIds';
import { FormRow } from './FormRow';
import { nameProblem } from './model';

export function NameField({
  name,
  readOnly,
  onChange,
}: {
  name: string;
  readOnly: boolean;
  onChange(name: string): void;
}) {
  if (readOnly) {
    return (
      <FormRow label="Display name" htmlFor="settings-profile-name-text">
        <output
          id="settings-profile-name-text"
          data-testid={settingsTestIds.profileNameReadOnly}
          className="inline-flex h-7 items-center rounded-control border border-dashed border-edge bg-canvas px-2.5 text-[length:var(--text-sm)] text-ink-muted touch:block touch:h-9 touch:max-w-full touch:truncate touch:text-[length:var(--text-base)] touch:leading-[34px]"
        >
          {name}
        </output>
        <span className="text-[length:var(--text-xs)] text-ink-muted touch:text-[length:var(--text-sm)]">
          Your name comes from your sign-in provider.
        </span>
      </FormRow>
    );
  }
  const problem = nameProblem(name);
  return (
    <FormRow label="Display name" htmlFor="settings-profile-name">
      <input
        id="settings-profile-name"
        data-testid={settingsTestIds.profileName}
        className="field w-70 max-w-full touch:h-9 touch:w-full"
        value={name}
        autoComplete="name"
        onChange={(e) => onChange(e.target.value)}
      />
      {problem === null ? null : (
        <span
          data-testid={settingsTestIds.profileNameError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {problem}
        </span>
      )}
      <span className="text-[length:var(--text-xs)] text-ink-muted touch:text-[length:var(--text-sm)]">
        Shown on cards, comments, history and @mentions. Your initials come from it.
      </span>
    </FormRow>
  );
}
