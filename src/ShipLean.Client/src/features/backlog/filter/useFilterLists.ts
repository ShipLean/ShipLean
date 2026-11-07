import type { StateDto } from '../../../api/projects';
import type { StateRef } from '../../../api/refs';
import { isStoryLevel, typeName } from '../../../api/workItemTypes';
import type { PickLists } from '../../../criteria/valueOptions';
import type { Level } from '../../../routes';
import { useProjectMembers, useProjectSprints, useProjectStates } from '../../workItem/workItemApi';

/** The people, states and sprints a filter clause on this backlog can pick, from the project's shared queries. */
export function useFilterLists(projectId: string, level: Level): PickLists {
  const members = useProjectMembers(projectId).data;
  const states = useProjectStates(projectId).data;
  const sprints = useProjectSprints(projectId).data;
  return {
    users: members?.map((m) => m.user),
    states: states && levelStates(states, level),
    sprints,
  };
}

/**
 * The states a backlog's filter offers: only those of the types the backlog lists (every type has its own New, Active and so
 * on, and a state clause matches by id, so another type's New matches nothing here). A name two of those types share says
 * whose it is, as in "New (Bug)".
 */
export function levelStates(states: readonly StateDto[], level: Level): StateRef[] {
  const own = states.filter((s) =>
    level === 'features'
      ? s.type === 'feature'
      : level === 'epics'
        ? s.type === 'epic'
        : isStoryLevel(s.type),
  );
  const counts = new Map<string, number>();
  for (const s of own) counts.set(s.name, (counts.get(s.name) ?? 0) + 1);
  return own.map((s) => ({
    id: s.id,
    name: (counts.get(s.name) ?? 0) > 1 ? `${s.name} (${typeName(s.type)})` : s.name,
    category: s.category,
  }));
}
