import { settingsTestIds } from '../../testIds';

/** The server's message under the field it blames; nothing when the error is about another field. */
export function FieldError({ message }: { message: string | undefined }) {
  if (message === undefined) return null;
  return (
    <span
      role="alert"
      data-testid={settingsTestIds.integrationsFieldError}
      className="font-normal text-alert"
    >
      {message}
    </span>
  );
}
