import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { AgentPanelDto } from '../../api/agentSessions';
import { registerInvalidation } from '../../realtime';
import { useWorkItem, workItemKey } from '../workItem/workItemApi';

// Under the item's own key, so a reconnect's refetch of the project's queries covers it too.
export const agentSessionsKey = (projectId: string, itemKey: string) =>
  [...workItemKey(projectId, itemKey), 'agent-sessions'] as const;

const itemPath = (workItemId: string) => `/api/work-items/${encodeURIComponent(workItemId)}`;

export const fetchAgentPanel = (workItemId: string) =>
  api.get<AgentPanelDto>(`${itemPath(workItemId)}/agent-sessions`);

export const startAgent = (workItemId: string) =>
  api.post<{ sessionId: string; state: string }>(`${itemPath(workItemId)}/agent-session`);

export const stopAgent = (workItemId: string) =>
  api.post<void>(`${itemPath(workItemId)}/agent-session/stop`);

// The panel's one view model, refetched when an agent session on this item changes (a teammate's Stop or Start, or the agent
// writing an activity). This tab's own Start and Stop invalidate directly: the event of its own request never arrives here.
export function useAgentPanel(projectId: string, itemKey: string) {
  const queryClient = useQueryClient();
  const workItemId = useWorkItem(projectId, itemKey).data?.id;
  const key = agentSessionsKey(projectId, itemKey);
  const query = useQuery({
    queryKey: key,
    queryFn: () => fetchAgentPanel(workItemId ?? ''),
    enabled: workItemId !== undefined,
  });
  useEffect(() => {
    if (workItemId === undefined) return undefined;
    return registerInvalidation('agentSessionChanged', (event) => {
      if (event.workItemId === workItemId) void queryClient.invalidateQueries({ queryKey: key });
    });
  }, [workItemId, queryClient, key]);
  const refetch = () => queryClient.invalidateQueries({ queryKey: key });
  const start = useMutation({
    mutationFn: () => startAgent(workItemId ?? ''),
    onSuccess: refetch,
    onError: refetch,
  });
  const stop = useMutation({
    mutationFn: () => stopAgent(workItemId ?? ''),
    onSuccess: refetch,
    onError: refetch,
  });
  return { query, start, stop };
}
