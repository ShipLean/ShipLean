import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../../api/client';
import type { AgentConnectionDto, AgentConnectionIssuedDto, AgentConnectionPut } from './model';

export const agentConnectionsKey = ['instance', 'agent-connections'] as const;

const route = '/api/instance/agent-connections';

/** Like the other instance tabs: no project is open, so no event reaches here; focus refetches and a change refetches. */
export function useAgentConnections() {
  return useQuery({
    queryKey: agentConnectionsKey,
    queryFn: () => api.get<AgentConnectionDto[]>(route),
    refetchOnWindowFocus: true,
  });
}

function useChange<V, R>(call: (variables: V) => Promise<R>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: call,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: agentConnectionsKey }),
  });
}

export const useCreateConnection = () =>
  useChange((body: AgentConnectionPut) => api.post<AgentConnectionIssuedDto>(route, body));

export const useUpdateConnection = () =>
  useChange((v: { id: string; body: AgentConnectionPut }) =>
    api.put<AgentConnectionDto>(`${route}/${v.id}`, v.body),
  );

export const useRegenerateSecret = () =>
  useChange((id: string) => api.post<AgentConnectionIssuedDto>(`${route}/${id}/regenerate-secret`));

export const useRegenerateToken = () =>
  useChange((id: string) => api.post<AgentConnectionIssuedDto>(`${route}/${id}/regenerate-token`));

export const useArchiveConnection = () =>
  useChange((id: string) => api.post<AgentConnectionDto>(`${route}/${id}/archive`));
