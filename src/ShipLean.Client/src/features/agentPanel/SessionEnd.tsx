import type { AgentSessionDto } from '@shiplean/core/api/agentSessions';

// TODO: show why the session ended (the end text, the detail when the viewer may see it, the run log link).
export function SessionEnd(props: { session: AgentSessionDto }) {
  void props;
  return null;
}
