import type { AgentSessionDto, AgentSessionState } from '../../api/agentSessions';
import { Chip, type ChipTone } from '../../components/Chip';
import { agentPanelTestIds as ids } from './testIds';

const stateChip: Record<AgentSessionState, { tone: ChipTone; label: string }> = {
  submitted: { tone: 'muted', label: 'Starting' },
  working: { tone: 'accent', label: 'Working' },
  inputRequired: { tone: 'warn', label: 'Needs input' },
  completed: { tone: 'ok', label: 'Completed' },
  failed: { tone: 'danger', label: 'Failed' },
  canceled: { tone: 'muted', label: 'Canceled' },
  stale: { tone: 'warn', label: 'Stale' },
};

// Who ran the session, how it stands, and when it started and last did something (labels come from the server).
export function SessionHeader({ session }: { session: AgentSessionDto }) {
  const chip = stateChip[session.state];
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[length:var(--text-sm)]">
      <b data-testid={ids.connection} className="font-semibold">
        {session.connectionName}
      </b>
      <span data-testid={ids.state} data-state={session.state}>
        <Chip tone={chip.tone}>{chip.label}</Chip>
      </span>
      <span data-testid={ids.started} className="text-[length:var(--text-xs)] text-ink-muted">
        Started {session.startedLabel}
      </span>
      <span data-testid={ids.lastActivity} className="text-[length:var(--text-xs)] text-ink-muted">
        {session.endedLabel
          ? `Ended ${session.endedLabel}`
          : `Last activity ${session.lastActivityLabel}`}
      </span>
    </div>
  );
}
