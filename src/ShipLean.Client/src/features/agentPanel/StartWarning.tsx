import type { AgentStartAvailabilityDto } from '@shiplean/core/api/agentSessions';
import { agentPanelTestIds as ids } from './testIds';

/** Beside Start agent: the last delivery to the connection failed. Nothing at all when there is no warning. */
export function StartWarning({ availability }: { availability: AgentStartAvailabilityDto }) {
  if (availability.warning === null) return null;
  return (
    <p data-testid={ids.startWarning} className="m-0 text-[length:var(--text-sm)] text-warn">
      {availability.warning}
    </p>
  );
}
