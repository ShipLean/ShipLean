import type { AgentDeliveryAttempt, AgentSessionDto } from '@shiplean/core/api/agentSessions';
import { agentPanelTestIds as ids } from './testIds';

// What one try came back with: the receiver's status, the scrubbed error, or both. Text from the receiver is rendered as text.
function outcome(attempt: AgentDeliveryAttempt): string {
  const parts: string[] = [];
  if (attempt.httpStatus !== null) parts.push(`HTTP ${attempt.httpStatus}`);
  if (attempt.error) parts.push(attempt.error);
  return parts.join(' · ');
}

// The delivery attempts of a session that is no longer live, oldest first, for project admins (the server sends none to members).
// A row is the action, the try number, when it was sent, how it came back and when the next try was due. Never a body.
export function SessionDeliveries({ session }: { session: AgentSessionDto }) {
  if (session.deliveries.length === 0) return null;
  return (
    <details
      data-testid={ids.sessionDeliveries}
      className="rounded-control border border-edge/60 text-[length:var(--text-sm)]"
    >
      <summary className="cursor-pointer px-2 py-1.5 font-semibold">
        Delivery ({session.deliveries.length})
      </summary>
      <ol className="m-0 grid list-none gap-0 p-0">
        {session.deliveries.map((attempt) => (
          <li
            key={`${attempt.action}-${attempt.attempt}-${attempt.at}`}
            data-testid={ids.sessionDeliveryRow}
            className="flex min-w-0 flex-wrap gap-x-2 gap-y-0.5 border-t border-edge/60 px-2 py-1.5 [overflow-wrap:anywhere]"
          >
            <b className="font-semibold">{attempt.action}</b>
            <span className="text-ink-muted">attempt {attempt.attempt}</span>
            <span className="text-ink-muted">{attempt.atLabel}</span>
            <span className={attempt.error ? 'text-alert' : undefined}>{outcome(attempt)}</span>
            {attempt.nextAttemptLabel ? (
              <span className="text-ink-muted">next try {attempt.nextAttemptLabel}</span>
            ) : null}
          </li>
        ))}
      </ol>
    </details>
  );
}
