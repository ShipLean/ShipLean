import type { DeviceSessionDto } from '@shiplean/core/api/deviceSessions';
import { devicesTestIds as ids } from './testIds';

function PhoneIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 16 16"
      className="mt-0.5 h-4 w-4 flex-none self-start text-ink-muted"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
    >
      <rect x="4.5" y="1.5" width="7" height="13" rx="1.5" />
      <path d="M7 12.5h2" strokeLinecap="round" />
    </svg>
  );
}

// One signed-in phone: its name, the prefix of its session (never the secret) and the server's own labels.
export function DeviceRow({
  device,
  disabled,
  onSignOut,
}: {
  device: DeviceSessionDto;
  disabled: boolean;
  onSignOut: () => void;
}) {
  return (
    <li
      data-testid={ids.row}
      className="flex items-center gap-3 border-b border-edge px-3 py-2.5 max-sm:flex-wrap"
    >
      <PhoneIcon />
      <div className="grid min-w-0 flex-1 gap-0.5 text-[length:var(--text-sm)]">
        <b data-testid={ids.name} className="break-words">
          {device.name}
        </b>
        <code
          data-testid={ids.prefix}
          className="font-mono text-[length:var(--text-xs)] text-ink-muted"
        >
          {device.tokenPrefix}…
        </code>
        <span data-testid={ids.when} className="text-[length:var(--text-xs)] text-ink-muted">
          Last used {device.lastUsedLabel} · Signed in {device.createdLabel}
        </span>
      </div>
      <button
        type="button"
        data-testid={ids.signOut}
        className="btn-ghost"
        disabled={disabled}
        aria-label={`Sign out ${device.name}`}
        onClick={onSignOut}
      >
        Sign out
      </button>
    </li>
  );
}
