import type { BacklogMode, Level } from '../routes';

// The backlog's react-query keys. Everything is keyed under ['projects', projectId, 'backlog', …] so one
// invalidation reaches every mode, and work item writes can invalidate the backlog without importing the feature.
export const backlogRootKey = (projectId: string) => ['projects', projectId, 'backlog'] as const;
export const backlogKey = (
  projectId: string,
  level: Level,
  mode: BacklogMode,
  filter: string,
  includeCompleted: boolean,
) => [...backlogRootKey(projectId), level, mode, filter, includeCompleted] as const;
