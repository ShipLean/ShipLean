import { useSyncExternalStore } from 'react';
import { componentTestIds } from './testIds';
import { Toast } from './Toast';
import { getToasts, subscribeToasts } from './toastStore';

export const DEFAULT_TOAST_DURATION_MS = 4000;

/** Renders the toasts raised by `showToast`. Mount once near the app root. Toasts never take focus. */
export function ToastHost({ durationMs = DEFAULT_TOAST_DURATION_MS }: { durationMs?: number }) {
  const toasts = useSyncExternalStore(subscribeToasts, getToasts);
  return (
    <div
      data-testid={componentTestIds.toastHost}
      role="region"
      aria-live="polite"
      aria-label="Notifications"
      className="pointer-events-none fixed bottom-5 left-1/2 z-40 flex -translate-x-1/2 flex-col gap-2"
    >
      {toasts.map((toast) => (
        <Toast key={toast.id} toast={toast} durationMs={durationMs} />
      ))}
    </div>
  );
}
