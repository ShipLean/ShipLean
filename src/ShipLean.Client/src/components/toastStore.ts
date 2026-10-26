export type ToastTone = 'default' | 'ok' | 'warn' | 'danger';

export interface ToastAction {
  label: string;
  onClick: () => void;
}

export interface ToastItem {
  id: number;
  message: string;
  tone: ToastTone;
  action?: ToastAction;
}

let toasts: ToastItem[] = [];
let nextId = 1;
const listeners = new Set<() => void>();

function publish(next: ToastItem[]) {
  toasts = next;
  listeners.forEach((listener) => listener());
}

export function subscribeToasts(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getToasts(): ToastItem[] {
  return toasts;
}

export function dismissToast(id: number) {
  publish(toasts.filter((toast) => toast.id !== id));
}

/** Adds a toast; a mounted `ToastHost` shows it and dismisses it after its `durationMs`. */
export function showToast(
  message: string,
  options: { tone?: ToastTone; action?: ToastAction } = {},
) {
  publish([
    ...toasts,
    { id: nextId++, message, tone: options.tone ?? 'default', action: options.action },
  ]);
}
