import { useQuery } from '@tanstack/react-query';
import type { PickLists } from '../../../../criteria/valueOptions';
import type { BoardTypeStatesDto } from '../../../../api/boards';
import type { StateRef } from '../../../../api/refs';
import {
  fetchMembers,
  fetchSprints,
  projectMembersKey,
  projectSprintsKey,
} from '../../../../queries/shared';

/** The people, states and sprints a lane's criteria can pick, from the project's shared queries. */
export function useLanePickLists(projectId: string, states: BoardTypeStatesDto[]): PickLists {
  const members = useQuery({
    queryKey: projectMembersKey(projectId),
    queryFn: () => fetchMembers(projectId),
  });
  const sprints = useQuery({
    queryKey: projectSprintsKey(projectId),
    queryFn: () => fetchSprints(projectId),
  });
  const seen = new Map<string, StateRef>();
  for (const entry of states) for (const state of entry.states) seen.set(state.id, state);
  return {
    users: members.data?.map((m) => m.user),
    sprints: sprints.data,
    states: [...seen.values()],
  };
}
