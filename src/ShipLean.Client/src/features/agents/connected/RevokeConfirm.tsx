import { useEffect, useRef } from 'react';
import type { ConnectedAgentDto } from '@shiplean/core/api/agents';
import { agentsTestIds as ids } from '../testIds';

/** The question under an agent's row, like the one for leaving a project. */
export function RevokeConfirm({
  agent,
  revoking,
  onConfirm,
  onCancel,
}: {
  agent: ConnectedAgentDto;
  revoking: boolean;
  onConfirm(): void;
  onCancel(): void;
}) {
  const cancel = useRef<HTMLButtonElement>(null);
  // The Revoke button that opened this question is disabled, so keyboard focus moves here.
  useEffect(() => cancel.current?.focus(), []);
  return (
    <li
      data-testid={ids.connectedRevokeRow}
      className="flex flex-wrap items-center gap-2 border-b border-edge bg-canvas px-3 py-2 text-[length:var(--text-sm)] last:border-b-0 touch:p-3"
    >
      <span className="touch:basis-full">
        Revoke <b>{agent.clientName}</b>? It stops working straight away, even mid-task. To use it
        again, connect it again.
      </span>
      <span className="flex-1 touch:hidden" />
      <button
        ref={cancel}
        type="button"
        data-testid={ids.connectedRevokeCancel}
        className="btn-ghost touch:h-[var(--touch-target)] touch:flex-1"
        onClick={onCancel}
      >
        Cancel
      </button>
      <button
        type="button"
        data-testid={ids.connectedRevokeConfirm}
        className="btn-signal disabled:opacity-50 touch:border touch:border-transparent touch:h-[var(--touch-target)] touch:flex-1"
        disabled={revoking}
        onClick={onConfirm}
      >
        Revoke
      </button>
    </li>
  );
}
