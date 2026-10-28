import { settingsTestIds } from '../../testIds';

/** The server's message under the field it blames; nothing when the error is about another field. */
export function FieldError({
  message,
  testId = settingsTestIds.integrationsFieldError,
}: {
  message: string | undefined;
  testId?: string;
}) {
  if (message === undefined) return null;
  return (
    <span role="alert" data-testid={testId} className="font-normal text-alert">
      {message}
    </span>
  );
}
