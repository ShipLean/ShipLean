import { useQuery } from '@tanstack/react-query';
import { fetchSprints, projectSprintsKey } from '../../../queries/shared';

/** Sprint id -> the dates the server labels it with ("20 Oct – 2 Nov"), from the sprints the shell already holds. */
export function useSprintDates(projectId: string): ReadonlyMap<string, string> {
  const sprints = useQuery({
    queryKey: projectSprintsKey(projectId),
    queryFn: () => fetchSprints(projectId),
  });
  return new Map((sprints.data ?? []).map((sprint) => [sprint.id, sprint.datesLabel]));
}
