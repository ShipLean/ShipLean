import type { AgentDeliveryAttempt } from '@shiplean/core/api/agentSessions';
import { agentPanelTestIds as ids } from './testIds';

// What one try came back with: the receiver's status, the scrubbed error, or both. Text from the receiver is rendered as text.
function outcome(attempt: AgentDeliveryAttempt): string {
  const parts: string[] = [];
  if (attempt.httpStatus !== null) parts.push(`HTTP ${attempt.httpStatus}`);
  if (attempt.error) parts.push(attempt.error);
  return parts.join(' · ');
}

// One delivery try: the action, the try number, when it was sent, how it came back and when the next try was due. Never a body.
export function DeliveryAttemptRow({ attempt }: { attempt: AgentDeliveryAttempt }) {
  return (
    <li
      data-testid={ids.sessionDeliveryRow}
      className="flex min-w-0 flex-wrap gap-x-2 gap-y-0.5 border-t border-edge/60 px-2 py-1.5 [overflow-wrap:anywhere]"
    >
      <b className="font-semibold">{attempt.action}</b>
      <span className="text-ink-muted">attempt {attempt.attempt}</span>
      <span className="text-ink-muted">{attempt.atLabel}</span>
      <span
        data-testid={ids.sessionDeliveryOutcome}
        className={attempt.error ? 'text-alert' : undefined}
      >
        {outcome(attempt)}
      </span>
      {attempt.nextAttemptLabel ? (
        <span className="text-ink-muted">next try {attempt.nextAttemptLabel}</span>
      ) : null}
    </li>
  );
}
