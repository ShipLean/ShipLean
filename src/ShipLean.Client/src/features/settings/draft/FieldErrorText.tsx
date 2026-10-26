import { settingsTestIds } from '../testIds';
import type { DraftField, FieldError } from './model';

// The message under a field, shown once Save has found it.
export function FieldErrorText({ errors, field }: { errors: FieldError[]; field: DraftField }) {
  const error = errors.find((e) => e.field === field);
  if (error === undefined) return null;
  return (
    <div
      id={`settings-error-${field}`}
      data-testid={settingsTestIds.fieldError}
      data-field={field}
      role="alert"
      className="mt-1 text-[length:var(--text-sm)] text-alert"
    >
      {error.message}
    </div>
  );
}
