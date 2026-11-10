import { getSprints, projectMetaKeys } from '@shiplean/core/api/projectMeta';
import {
  fetchRetroActionDialog,
  fetchRetroFeatures,
  fetchRetroInsights,
  fetchSprintRetro,
  retroKeys,
  type RetroActionDialogQuery,
  type RetroRange,
} from '@shiplean/core/api/retrosApi';
import { useQuery, useQueryClient } from '@tanstack/react-query';

export const useSprintsQuery = (projectId: string) =>
  useQuery({ queryKey: projectMetaKeys.sprints(projectId), queryFn: () => getSprints(projectId) });

export const useSprintRetroQuery = (sprintId: string) =>
  useQuery({ queryKey: retroKeys.sprint(sprintId), queryFn: () => fetchSprintRetro(sprintId) });

export const useRetroInsightsQuery = (projectId: string, range: RetroRange) =>
  useQuery({
    queryKey: retroKeys.insights(projectId, range),
    queryFn: () => fetchRetroInsights(projectId, range),
  });

export const useRetroFeaturesQuery = (projectId: string) =>
  useQuery({
    queryKey: retroKeys.features(projectId),
    queryFn: () => fetchRetroFeatures(projectId),
  });

export const useRetroActionDialogQuery = (projectId: string, query: RetroActionDialogQuery) =>
  useQuery({
    queryKey: retroKeys.actionDialog(projectId, query),
    queryFn: () => fetchRetroActionDialog(projectId, query),
  });

// Refreshes every Retro read of the project: the sprint retros, insights, features and action dialogs.
export function useInvalidateRetro(): (projectId: string) => Promise<void> {
  const queryClient = useQueryClient();
  return async (projectId) => {
    // The factories' own prefixes: ['retro', 'sprint'] and ['projects', p, 'retros'].
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: retroKeys.sprint('').slice(0, 2) }),
      queryClient.invalidateQueries({ queryKey: retroKeys.features(projectId).slice(0, 3) }),
    ]);
  };
}
