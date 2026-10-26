import {
  isInstanceLevel,
  routePath,
  type MatchedRoute,
  type RouteId,
  type RouteParams,
} from '../../routes';

// Where picking a project goes: the screen you are on, in that project (same route id, same query). A sprint's pages
// fall back to All sprints (the sprint is another project's), a work item or its attachment to the Stories backlog, and
// anything that is not a project screen (an install-level page, an unknown address) to the Stories backlog too.
export function sameScreenIn(route: MatchedRoute | undefined, key: string): string {
  if (route === undefined || isInstanceLevel(route.id)) return routePath('storiesBacklog', { key });
  switch (route.id) {
    case 'sprintBacklog':
    case 'taskboard':
    case 'capacity':
      return routePath('sprints', { key });
    case 'workItem':
    case 'workItemAttachment':
      return routePath('storiesBacklog', { key });
    default:
      return routePath(route.id, { ...route.params, key } as RouteParams[RouteId], route.query);
  }
}
