import type { PickLists } from '../../../criteria/valueOptions';
import { useProjectMembers, useProjectSprints, useProjectStates } from '../../workItem/workItemApi';

/** The people, states and sprints a filter clause can pick, from the project's shared queries. */
export function useFilterLists(projectId: string): PickLists {
  const members = useProjectMembers(projectId).data;
  const states = useProjectStates(projectId).data;
  const sprints = useProjectSprints(projectId).data;
  return {
    users: members?.map((m) => m.user),
    states,
    sprints,
  };
}
