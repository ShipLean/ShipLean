import { useState } from 'react';
import { ToastHost } from '../ToastHost';
import { harnessTestIds } from './harnessIds';
import { showToast } from '../toastStore';

/** CT-only: a button that raises toasts from inside the browser, beside a host. */
export function ToastHarness({
  message,
  actionLabel,
  durationMs,
}: {
  message: string;
  actionLabel?: string;
  durationMs?: number;
}) {
  const [actions, setActions] = useState(0);
  return (
    <>
      <button
        type="button"
        data-testid={harnessTestIds.ctShowToast}
        onClick={() =>
          showToast(message, {
            action: actionLabel
              ? { label: actionLabel, onClick: () => setActions((n) => n + 1) }
              : undefined,
          })
        }
      >
        Show
      </button>
      <span data-testid={harnessTestIds.ctToastActionCount}>{actions}</span>
      <ToastHost durationMs={durationMs} />
    </>
  );
}
