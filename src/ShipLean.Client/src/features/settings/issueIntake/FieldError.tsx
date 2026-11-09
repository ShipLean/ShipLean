import type { ApiRequestError } from '@shiplean/core/api/client';
import { settingsTestIds as ids } from '../testIds';

/** The id a field's error carries, so the control can name it in aria-describedby. */
export const fieldErrorId = (scope: string, field: string) => `${scope}-error-${field}`;

/** The field a refusal names; the error body carries it (ApiRequestError.body.field). */
export function errorField(error: ApiRequestError): string | undefined {
  const body = error.body as { field?: unknown } | undefined;
  return typeof body?.field === 'string' ? body.field : undefined;
}

/** A save refusal shown beside the field it names. */
export function FieldError({ id, field, message }: { id: string; field: string; message: string }) {
  return (
    <div
      id={id}
      role="alert"
      data-testid={ids.issueIntakeError}
      data-field={field}
      className="text-alert"
    >
      {message}
    </div>
  );
}
