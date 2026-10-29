import { useQuery } from '@tanstack/react-query';
import type { AgentConnectionOption } from '../../../../api/projects';
import { fetchProjectSettings, projectSettingsKey } from '../../../settings/settingsApi';

/** What the connection select offers: the non-archived connections and the project's default, by name. */
export interface AgentConnections {
  options: AgentConnectionOption[];
  /** The project's own connection, or null when it has none (or the options haven't loaded). */
  defaultName: string | null;
}

/** Project settings General already returns these and any member may read them, as board settings editors are. */
export function useAgentConnections(projectId: string): AgentConnections {
  const settings = useQuery({
    queryKey: projectSettingsKey(projectId),
    queryFn: () => fetchProjectSettings(projectId),
  });
  const general = settings.data?.general;
  const options = general?.agentConnections ?? [];
  const defaultName = options.find((o) => o.id === general?.agentConnectionId)?.name ?? null;
  return { options: options.filter((o) => !o.archived), defaultName };
}
