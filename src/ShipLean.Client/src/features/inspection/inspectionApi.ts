import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchAgentRuns,
  fetchAgentSetup,
  fetchInspectionSettings,
  fetchRunPage,
  fetchRunRow,
  putInspectionSettings,
  type InspectionProjectSettingsPut,
} from '@shiplean/core/api/inspection';

// Every inspection query lives under the project key, so one invalidation reaches them all. `project` is the project key.
export const inspectionKey = (project: string) => ['inspection', project] as const;
export const agentRunsKey = (project: string, itemKey: string) =>
  [...inspectionKey(project), 'runs', itemKey] as const;
export const runPageKey = (project: string, itemKey: string, runId: string, errorsOnly: boolean) =>
  [...inspectionKey(project), 'run', itemKey, runId, { errorsOnly }] as const;
export const runRowKey = (project: string, itemKey: string, runId: string, rowId: string) =>
  [...inspectionKey(project), 'row', itemKey, runId, rowId] as const;
export const inspectionSettingsKey = (project: string) =>
  [...inspectionKey(project), 'settings'] as const;
export const agentSetupKey = (project: string, agent: string, itemKey: string) =>
  [...inspectionKey(project), 'agent-setup', agent, itemKey] as const;

export const useAgentRuns = (project: string, itemKey: string) =>
  useQuery({
    queryKey: agentRunsKey(project, itemKey),
    queryFn: () => fetchAgentRuns(project, itemKey),
  });

/** The run page, fetched once per errors-only setting; zooming a turn is the page's own business and fetches nothing. */
export const useRunPage = (project: string, itemKey: string, runId: string, errorsOnly: boolean) =>
  useQuery({
    queryKey: runPageKey(project, itemKey, runId, errorsOnly),
    queryFn: () => fetchRunPage(project, itemKey, runId, { errorsOnly }),
    // Toggling errors only keeps the page (and the find box) up on the previous data while the new one loads.
    placeholderData: keepPreviousData,
  });

/** One row's detail, fetched when it is selected. */
export const useRunRow = (project: string, itemKey: string, runId: string, rowId: string) =>
  useQuery({
    queryKey: runRowKey(project, itemKey, runId, rowId),
    queryFn: () => fetchRunRow(project, itemKey, runId, rowId),
  });

export const useInspectionSettings = (project: string) =>
  useQuery({
    queryKey: inspectionSettingsKey(project),
    queryFn: () => fetchInspectionSettings(project),
  });

export function useSaveInspectionSettings(project: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: InspectionProjectSettingsPut) => putInspectionSettings(project, body),
    onSuccess: (saved) => {
      queryClient.setQueryData(inspectionSettingsKey(project), saved);
    },
  });
}

export const useAgentSetup = (project: string, agent: string, itemKey: string) =>
  useQuery({
    queryKey: agentSetupKey(project, agent, itemKey),
    queryFn: () => fetchAgentSetup(project, agent, itemKey === '' ? undefined : itemKey),
  });
