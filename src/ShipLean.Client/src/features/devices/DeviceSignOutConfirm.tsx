import type { DeviceSessionDto } from '@shiplean/core/api/deviceSessions';
import { devicesTestIds as ids } from './testIds';

// The warn row under a phone: Sign out asks first, like Revoke on the agents.
export function DeviceSignOutConfirm({
  device,
  pending,
  onCancel,
  onConfirm,
}: {
  device: DeviceSessionDto;
  pending: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <li
      data-testid={ids.confirm}
      className="flex flex-wrap gap-2 border-b border-edge bg-[color-mix(in_srgb,var(--color-warn)_10%,var(--color-surface))] px-3 py-2.5 text-[length:var(--text-sm)]"
    >
      <span className="min-w-[200px] flex-1">
        Sign out <b>{device.name}</b>? The app there goes back to Sign in straight away. Its server
        and email stay filled in.
      </span>
      <span className="flex gap-2 max-sm:w-full max-sm:flex-col">
        <button
          type="button"
          data-testid={ids.confirmCancel}
          className="btn-ghost"
          disabled={pending}
          onClick={onCancel}
        >
          Cancel
        </button>
        <button
          type="button"
          data-testid={ids.confirmGo}
          className="btn-ghost text-alert hover:text-alert"
          disabled={pending}
          onClick={onConfirm}
        >
          Sign out
        </button>
      </span>
    </li>
  );
}
