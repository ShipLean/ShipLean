import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../../api/client';
import type { IntegrationsSaveDto, IntegrationsSettingsDto } from './model';

export const integrationsKey = ['instance', 'integrations'] as const;

/** Like the General tab: no project is open, so no event reaches here; focus refetches and a save fills the cache. */
export function useIntegrations() {
  return useQuery({
    queryKey: integrationsKey,
    queryFn: () => api.get<IntegrationsSettingsDto>('/api/instance/integrations'),
    refetchOnWindowFocus: true,
  });
}

export function useSaveIntegrations() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: IntegrationsSaveDto) =>
      api.put<IntegrationsSettingsDto>('/api/instance/integrations', body),
    onSuccess: (saved) => {
      queryClient.setQueryData(integrationsKey, saved);
    },
  });
}
