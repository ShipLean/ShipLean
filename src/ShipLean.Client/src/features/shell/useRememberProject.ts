import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import type { ProjectSummaryDto } from '../../api/projects';
import { projectsKey } from '../../queries/shared';
import { recentProjectsKey, saveRecentProjectIds, withRecent } from './shellApi';
import { useRecentProjectIds } from './useShellQueries';

// Opening a project puts it first in the recentProjects preference, then refreshes the project list. Saves run one
// after the other, and each works out the next list from the cache when its turn comes (the one before has landed
// by then), so opening A and then B quickly keeps both.
export function useRememberProject(project: ProjectSummaryDto | undefined) {
  const client = useQueryClient();
  const recent = useRecentProjectIds().data;
  const save = useMutation({
    scope: { id: 'recentProjects' },
    mutationFn: async (projectId: string) => {
      const current = client.getQueryData<string[]>(recentProjectsKey) ?? [];
      if (current[0] === projectId) return;
      await saveRecentProjectIds(withRecent(current, projectId));
    },
    onSettled: () =>
      Promise.all([
        client.invalidateQueries({ queryKey: projectsKey, exact: true }),
        client.invalidateQueries({ queryKey: recentProjectsKey }),
      ]),
  });
  const { mutate } = save;
  const projectId = project?.id;
  useEffect(() => {
    if (projectId === undefined || recent === undefined || recent[0] === projectId) return;
    mutate(projectId);
  }, [projectId, recent, mutate]);
}
