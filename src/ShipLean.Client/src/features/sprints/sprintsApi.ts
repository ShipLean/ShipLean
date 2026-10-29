import { useEffect } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { WorkItemChanges, WorkItemDto } from '../../api/workItems';
import type {
  RolloverDto,
  RolloverRequest,
  SprintCapacityDto,
  SprintCapacityPut,
  SprintCreate,
  SprintDto,
  SprintMetricsDto,
  SprintUpdate,
  VelocityDto,
} from '../../api/sprints';
import { showToast } from '../../components/toastStore';
import { registerInvalidation } from '../../realtime';
import { projectMembersKey, projectSprintsKey } from '../../queries/shared';
import { backlogRootKey } from '../backlog/backlogApi';
import { patchWorkItem } from '../workItem/workItemApi';

const projectPath = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}`;

/** GET /api/projects/{id}/velocity. */
export const velocityKey = (projectId: string) => ['projects', projectId, 'velocity'] as const;

/** GET /api/projects/{id}/sprints/next: under the sprints key, so a created sprint refreshes it too. */
export const nextSprintKey = (projectId: string) =>
  [...projectSprintsKey(projectId), 'next'] as const;

/** GET /api/sprints/{id}/metrics. */
export const sprintMetricsKey = (projectId: string, sprintId: string) =>
  ['projects', projectId, 'sprint-metrics', sprintId] as const;

/** GET /api/sprints/{id}/capacity. */
export const sprintCapacityKey = (projectId: string, sprintId: string) =>
  ['projects', projectId, 'sprint-capacity', sprintId] as const;

export const fetchVelocity = (projectId: string) =>
  api.get<VelocityDto>(`${projectPath(projectId)}/velocity`);

export const fetchNextSprint = (projectId: string) =>
  api.get<SprintCreate>(`${projectPath(projectId)}/sprints/next`);

export const fetchSprintMetrics = (sprintId: string) =>
  api.get<SprintMetricsDto>(`/api/sprints/${encodeURIComponent(sprintId)}/metrics`);

export const fetchSprintCapacity = (sprintId: string) =>
  api.get<SprintCapacityDto>(`/api/sprints/${encodeURIComponent(sprintId)}/capacity`);

export const createSprint = (projectId: string, create: SprintCreate) =>
  api.post<SprintDto>(`${projectPath(projectId)}/sprints`, create);

// What changes the figures the All sprints page shows: sprints and their capacity, the project's velocity settings, and
// any work item's points, sprint or state. Own writes invalidate directly (a tab ignores its own events).
const WIDE_FIGURE_EVENTS = [
  'projectChanged',
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemRemoved',
] as const;

/** Refetches the sprint list, its defaults and the sprint figures when another tab or person changes them. */
export function useSprintsRealtime(projectId: string) {
  const queryClient = useQueryClient();
  useEffect(() => {
    // A sprint event names its sprint, so only that sprint's figures go stale; the others carry no sprint.
    const refetch = (sprintId?: string) => {
      void queryClient.invalidateQueries({ queryKey: projectSprintsKey(projectId) });
      void queryClient.invalidateQueries({ queryKey: velocityKey(projectId) });
      void queryClient.invalidateQueries({
        queryKey:
          sprintId === undefined
            ? ['projects', projectId, 'sprint-metrics']
            : sprintMetricsKey(projectId, sprintId),
      });
      void queryClient.invalidateQueries({
        queryKey:
          sprintId === undefined
            ? ['projects', projectId, 'sprint-capacity']
            : sprintCapacityKey(projectId, sprintId),
      });
    };
    const unregister = [
      registerInvalidation('sprintChanged', (event) => refetch(event.sprintId)),
      ...WIDE_FIGURE_EVENTS.map((type) => registerInvalidation(type, () => refetch())),
    ];
    return () => unregister.forEach((off) => off());
  }, [projectId, queryClient]);
}

export function useVelocity(projectId: string) {
  return useQuery({ queryKey: velocityKey(projectId), queryFn: () => fetchVelocity(projectId) });
}

export interface SprintFigures {
  metrics: SprintMetricsDto | undefined;
  capacity: SprintCapacityDto | undefined;
}

/** Each sprint's committed/completed (metrics) and entered capacity, by sprint id, as they load. */
export function useSprintFigures(
  projectId: string,
  sprints: SprintDto[],
): ReadonlyMap<string, SprintFigures> {
  const metrics = useQueries({
    queries: sprints.map((sprint) => ({
      queryKey: sprintMetricsKey(projectId, sprint.id),
      queryFn: () => fetchSprintMetrics(sprint.id),
    })),
  });
  const capacity = useQueries({
    queries: sprints.map((sprint) => ({
      queryKey: sprintCapacityKey(projectId, sprint.id),
      queryFn: () => fetchSprintCapacity(sprint.id),
    })),
  });
  return new Map(
    sprints.map((sprint, index) => [
      sprint.id,
      { metrics: metrics[index]?.data, capacity: capacity[index]?.data },
    ]),
  );
}

export function useNextSprint(projectId: string) {
  return useQuery({
    queryKey: nextSprintKey(projectId),
    queryFn: () => fetchNextSprint(projectId),
  });
}

/** Creates a sprint, then refreshes the list (and with it the defaults and every figure that follows the list). */
export function useCreateSprint(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (create: SprintCreate) => createSprint(projectId, create),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: projectSprintsKey(projectId) });
      showToast(`Created ${created.name}`, { tone: 'ok' });
    },
  });
}

/** GET /api/projects/{id}/work-items: every live item with its children, the Sprint backlog table's rows. */
export const sprintItemsKey = (projectId: string) =>
  ['projects', projectId, 'sprint-items'] as const;

export const fetchProjectItems = (projectId: string) =>
  api.get<WorkItemDto[]>(`${projectPath(projectId)}/work-items`);

export const updateSprint = (sprintId: string, update: SprintUpdate) =>
  api.patch<SprintDto>(`/api/sprints/${encodeURIComponent(sprintId)}`, update);

/**
 * Saves the sprint's name, dates and goal. The sprint list feeds the header and the rows; new dates also move the
 * burndown (metrics), the backlog's planning sections and the sprint items, so those are refetched directly (a tab
 * ignores its own events).
 */
export function useUpdateSprint(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (update: SprintUpdate) => updateSprint(sprintId, update),
    onSuccess: async (saved) => {
      await Promise.all(
        [
          projectSprintsKey(projectId),
          velocityKey(projectId),
          sprintItemsKey(projectId),
          backlogRootKey(projectId),
          ['projects', projectId, 'sprint-metrics'],
          ['projects', projectId, 'sprint-capacity'],
        ].map((queryKey) => queryClient.invalidateQueries({ queryKey })),
      );
      showToast(`Saved ${saved.name}`, { tone: 'ok' });
    },
  });
}

/** Refetches the Sprint backlog's items and figures when another tab or person changes a work item, the states or the order. */
export function useSprintBacklogRealtime(projectId: string) {
  useSprintsRealtime(projectId);
  const queryClient = useQueryClient();
  useEffect(() => {
    const refetchItems = () =>
      void queryClient.invalidateQueries({ queryKey: sprintItemsKey(projectId) });
    // A state's category decides Completed and Remaining, so a states change moves the figures too.
    const refetchAll = () => {
      refetchItems();
      void queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'sprint-metrics'] });
    };
    const unregister = [
      registerInvalidation('sprintChanged', refetchItems),
      registerInvalidation('workItemReordered', refetchItems),
      registerInvalidation('statesChanged', refetchAll),
      ...WIDE_FIGURE_EVENTS.map((type) => registerInvalidation(type, refetchItems)),
    ];
    return () => unregister.forEach((off) => off());
  }, [projectId, queryClient]);
}

export function useSprintMetrics(projectId: string, sprintId: string) {
  return useQuery({
    queryKey: sprintMetricsKey(projectId, sprintId),
    queryFn: () => fetchSprintMetrics(sprintId),
  });
}

export function useSprintCapacity(projectId: string, sprintId: string) {
  return useQuery({
    queryKey: sprintCapacityKey(projectId, sprintId),
    queryFn: () => fetchSprintCapacity(sprintId),
  });
}

export function useSprintItems(projectId: string) {
  return useQuery({
    queryKey: sprintItemsKey(projectId),
    queryFn: () => fetchProjectItems(projectId),
  });
}

/** Saves the sprint's goal, then refreshes the sprint list (the header reads the goal from it). */
export function useSaveSprintGoal(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (goal: string) =>
      updateSprint(sprintId, { name: null, startDate: null, endDate: null, goal }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectSprintsKey(projectId) }),
  });
}

const NO_CHANGES: WorkItemChanges = {
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

export interface StateChange {
  /** The row as the list loaded it: its revision is the write's expectedRevision. */
  item: WorkItemDto;
  stateId: string;
}

/**
 * Changes one item's state from its row. Whatever the outcome the items, the sprint's figures and its capacity are
 * refetched directly (a tab ignores its own events), which also replaces a row a stale write found out of date.
 */
export function useChangeItemState(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ item, stateId }: StateChange) =>
      patchWorkItem(item.id, {
        expectedRevision: item.revision,
        changes: { ...NO_CHANGES, state: { value: stateId } },
      }),
    // An unassigned item moved into progress is assigned to the mover by the server, which says so in the notices.
    onSuccess: (result) => {
      if (result.notices.includes('pulled')) showToast('Assigned to you', { tone: 'ok' });
    },
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: sprintItemsKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: sprintMetricsKey(projectId, sprintId) }),
        queryClient.invalidateQueries({ queryKey: sprintCapacityKey(projectId, sprintId) }),
      ]),
  });
}

export const putSprintCapacity = (sprintId: string, put: SprintCapacityPut) =>
  api.put<SprintCapacityDto>(`/api/sprints/${encodeURIComponent(sprintId)}/capacity`, put);

/** The Capacity page's reads: the sprint figures, plus the member list a project change (members joining or leaving) alters. */
export function useCapacityRealtime(projectId: string) {
  useSprintsRealtime(projectId);
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('projectChanged', () => {
        void queryClient.invalidateQueries({ queryKey: projectMembersKey(projectId) });
      }),
    [projectId, queryClient],
  );
}

/**
 * Saves the sprint's capacity. The response is the saved set, so it goes straight into the cache; the lists that show
 * capacity (the sprint list, the backlog's planning sections) are refetched directly, as a tab ignores its own events.
 */
export function useSaveSprintCapacity(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (put: SprintCapacityPut) => putSprintCapacity(sprintId, put),
    onSuccess: async (saved) => {
      queryClient.setQueryData(sprintCapacityKey(projectId, sprintId), saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: projectSprintsKey(projectId) }),
      ]);
      showToast('Capacity saved', { tone: 'ok' });
    },
  });
}

/** GET /api/sprints/{id}/rollover: the ended sprint's unfinished stories and bugs, and the next sprint's room. */
export const sprintRolloverKey = (projectId: string, sprintId: string) =>
  ['projects', projectId, 'sprint-rollover', sprintId] as const;

export const fetchRollover = (sprintId: string) =>
  api.get<RolloverDto>(`/api/sprints/${encodeURIComponent(sprintId)}/rollover`);

export const postRollover = (sprintId: string, request: RolloverRequest) =>
  api.post<SprintDto>(`/api/sprints/${encodeURIComponent(sprintId)}/rollover`, request);

export function useRollover(projectId: string, sprintId: string | undefined) {
  return useQuery({
    queryKey: sprintRolloverKey(projectId, sprintId ?? ''),
    queryFn: () => fetchRollover(sprintId!),
    enabled: sprintId !== undefined,
  });
}

/**
 * Refetches the sprint list and the rollover when another person decides it, or changes a sprint or an item (an item
 * moved or closed elsewhere changes the unfinished list).
 */
export function useSprintEndRealtime(projectId: string) {
  const queryClient = useQueryClient();
  useEffect(() => {
    const refetch = () => {
      void queryClient.invalidateQueries({ queryKey: projectSprintsKey(projectId) });
      void queryClient.invalidateQueries({
        queryKey: ['projects', projectId, 'sprint-rollover'],
      });
    };
    const unregister = [
      registerInvalidation('sprintChanged', refetch),
      ...WIDE_FIGURE_EVENTS.map((type) => registerInvalidation(type, refetch)),
    ];
    return () => unregister.forEach((off) => off());
  }, [projectId, queryClient]);
}

/**
 * Applies the rollover. It is decided for everyone, so on success this tab refetches directly what the move changes (a
 * tab ignores its own events): the sprint list, the rollover, the sprint pages' items and figures, the backlogs.
 */
export function useApplyRollover(projectId: string, sprintId: string, onApplied: () => void) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RolloverRequest) => postRollover(sprintId, request),
    // Before the refetches below, so this tab never shows the 'someone else did it' state for its own apply.
    onSuccess: onApplied,
    onSettled: () =>
      Promise.all(
        [
          projectSprintsKey(projectId),
          sprintRolloverKey(projectId, sprintId),
          sprintItemsKey(projectId),
          velocityKey(projectId),
          ['projects', projectId, 'sprint-metrics'],
          ['projects', projectId, 'sprint-capacity'],
          backlogRootKey(projectId),
        ].map((queryKey) => queryClient.invalidateQueries({ queryKey })),
      ),
  });
}
