import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@shiplean/core/api/client';
import type {
  AgentConnectionDto,
  AgentConnectionHealth,
  AgentConnectionIssuedDto,
  AgentConnectionPut,
  AgentConnectionTest,
} from './model';

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

export const agentConnectionHealthKey = (id: string) =>
  ['instance', 'agent-connections', id, 'health'] as const;

/** GET .../{id}/health: the last delivery and the failures of the last 24 hours. */
export const health = (id: string) => api.get<AgentConnectionHealth>(`${route}/${id}/health`);

/** POST .../{id}/test: a signed ping with no item data. Reached is true for any HTTP answer. */
export const test = (id: string) => api.post<AgentConnectionTest>(`${route}/${id}/test`);

/** The health read for one connection row. */
export const useConnectionHealth = (id: string) =>
  useQuery({ queryKey: agentConnectionHealthKey(id), queryFn: () => health(id) });

/** Test connection. A test is not stored, so nothing is invalidated. */
export const useTestConnection = () => useMutation({ mutationFn: test });
