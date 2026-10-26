import {
  fetchProjectByKey,
  fetchSprints,
  projectByKeyKey,
  projectSprintsKey,
} from '../queries/shared';

// The two reads every project page makes, under the shared react-query keys (queries/shared.ts). Retries are the
// app's QueryClient default (queries/retry.ts).
export const projectByKeyQuery = (key: string) => ({
  queryKey: projectByKeyKey(key),
  queryFn: () => fetchProjectByKey(key),
});

export const sprintsQuery = (projectId: string) => ({
  queryKey: projectSprintsKey(projectId),
  queryFn: () => fetchSprints(projectId),
});
