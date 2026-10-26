import { useEffect } from 'react';
import { componentTestIds } from './testIds';
import { dismissToast, type ToastItem } from './toastStore';

export function Toast({ toast, durationMs }: { toast: ToastItem; durationMs: number }) {
  useEffect(() => {
    const timer = setTimeout(() => dismissToast(toast.id), durationMs);
    return () => clearTimeout(timer);
  }, [toast.id, durationMs]);

  return (
    <div
      data-testid={componentTestIds.toast}
      data-tone={toast.tone}
      role={toast.tone === 'danger' ? 'alert' : undefined}
      className="pointer-events-auto flex items-center gap-2 rounded-panel bg-ink px-3.5 py-2 text-[length:var(--text-sm)] font-medium text-canvas shadow-panel"
      style={
        toast.tone === 'default' ? undefined : { borderLeft: `3px solid ${toneColour[toast.tone]}` }
      }
    >
      <span>{toast.message}</span>
      {toast.action ? (
        <button
          type="button"
          data-testid={componentTestIds.toastAction}
          className="font-semibold underline"
          onClick={() => {
            toast.action?.onClick();
            dismissToast(toast.id);
          }}
        >
          {toast.action.label}
        </button>
      ) : null}
      <button
        type="button"
        data-testid={componentTestIds.toastDismiss}
        aria-label="Dismiss"
        onClick={() => dismissToast(toast.id)}
      >
        ×
      </button>
    </div>
  );
}

const toneColour = {
  default: 'transparent',
  ok: 'var(--color-ok)',
  warn: 'var(--color-warn)',
  danger: 'var(--color-danger)',
} as const;
