import type { AgentSessionDto } from '@shiplean/core/api/agentSessions';
import { DeliveryAttemptRow } from './DeliveryAttemptRow';
import { agentPanelTestIds as ids } from './testIds';

// The delivery attempts of a session that is no longer live, oldest first, for project admins (the server sends none to members).
export function SessionDeliveries({ session }: { session: AgentSessionDto }) {
  if (session.deliveries.length === 0) return null;
  return (
    <details
      data-testid={ids.sessionDeliveries}
      className="rounded-control border border-edge/60 text-[length:var(--text-sm)]"
    >
      <summary
        data-testid={ids.sessionDeliveriesToggle}
        className="cursor-pointer px-2 py-1.5 font-semibold"
      >
        Delivery ({session.deliveries.length})
      </summary>
      <ol className="m-0 grid list-none gap-0 p-0">
        {session.deliveries.map((attempt) => (
          <DeliveryAttemptRow
            key={`${attempt.action}-${attempt.attempt}-${attempt.at}`}
            attempt={attempt}
          />
        ))}
      </ol>
    </details>
  );
}
