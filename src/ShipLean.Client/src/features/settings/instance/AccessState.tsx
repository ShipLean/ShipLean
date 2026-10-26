import type { ReactNode } from 'react';
import { settingsTestIds } from '../testIds';
import { errorText, isForbidden } from './model';

/** Loading, refused and failed states shared by the instance tabs; the children render once data has loaded. */
export function AccessState({
  pending,
  error,
  children,
}: {
  pending: boolean;
  error: unknown;
  children: ReactNode;
}) {
  if (pending) {
    return (
      <p data-testid={settingsTestIds.instanceLoading} className="p-4 text-ink-muted">
        Loading…
      </p>
    );
  }
  if (isForbidden(error)) {
    return (
      <div data-testid={settingsTestIds.instanceDenied} className="p-4">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold text-ink">
          Instance settings are for instance admins
        </h2>
        <p className="text-ink-muted">Ask an instance admin if you need something changed.</p>
      </div>
    );
  }
  if (error) {
    return (
      <p role="alert" data-testid={settingsTestIds.instanceLoadFailed} className="p-4 text-alert">
        {errorText(error)}
      </p>
    );
  }
  return <>{children}</>;
}
