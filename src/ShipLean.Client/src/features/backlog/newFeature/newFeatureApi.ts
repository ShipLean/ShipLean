import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type { WorkItemChanges, WorkItemCreate, WriteResult } from '../../../api/workItems';
import { backlogRootKey } from '../backlogApi';

// C#: SlugCheckDto. `reason` is set for an invalid slug; a taken one is just `available: false`.
export interface SlugCheck {
  value: string;
  available: boolean;
  reason?: string;
}

export const slugCheckKey = (projectId: string, value: string) =>
  ['projects', projectId, 'features', 'slug-check', value] as const;

/** Asks whether a Feature here may use `value`; off until there is something to check. */
export function useSlugCheck(projectId: string, value: string) {
  return useQuery({
    queryKey: slugCheckKey(projectId, value),
    queryFn: () =>
      api.get<SlugCheck>(
        `/api/projects/${encodeURIComponent(projectId)}/features/slug-check?value=${encodeURIComponent(value)}`,
      ),
    enabled: value !== '',
    // A slug can be taken between checks; never show an old answer for a value typed again.
    gcTime: 0,
  });
}

export interface NewFeature {
  title: string;
  parentId: string | null;
  // null = no slug; a Feature without one is found by its key only.
  slug: string | null;
  goal: string;
  statusLine: string;
  // The state to start in; null = the type's first Proposed state.
  stateId: string | null;
}

const noChanges: WorkItemChanges = {
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: null,
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
};

/** Creates the Feature through the ordinary create-work-item request; the store puts it first in the backlog. */
export function useCreateFeature(projectId: string, projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (feature: NewFeature) =>
      api.post<WriteResult>('/api/work-items', {
        projectId,
        type: 'feature',
        title: feature.title,
        parentId: feature.parentId,
        sprintId: null,
        changes: feature.stateId ? { ...noChanges, state: { value: feature.stateId } } : null,
        goal: feature.goal,
        statusLine: feature.statusLine,
        slug: feature.slug,
      } satisfies WorkItemCreate),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: ['projects', 'by-key', projectKey] }),
      ]);
    },
  });
}
