import type { AgentStartAvailabilityDto } from '@shiplean/core/api/agentSessions';

// TODO: show the availability warning (the last delivery to the connection failed) beside Start agent.
export function StartWarning(props: { availability: AgentStartAvailabilityDto }) {
  void props;
  return null;
}
