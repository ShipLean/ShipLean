import {
  isInstanceLevel,
  routePath,
  type MatchedRoute,
  type RouteId,
  type RouteParams,
} from '../../routes';

// Where picking a project goes: the screen you are on, in that project (same route id, same query). A sprint's pages
// fall back to All sprints (the sprint is another project's), a work item, its attachment or its Feature board to the Stories backlog, and
// anything that is not a project screen (an install-level page, an unknown address) to the Stories backlog too.
export function sameScreenIn(route: MatchedRoute | undefined, key: string): string {
  if (route === undefined || isInstanceLevel(route.id)) return routePath('storiesBacklog', { key });
  switch (route.id) {
    case 'sprintBacklog':
    case 'taskboard':
    case 'capacity':
    case 'sprintRetro':
    case 'sprintDemo':
      return routePath('sprints', { key });
    case 'retros':
      // Its filters and the open retro name the old project's sprints, themes and retros.
      return routePath('retros', { key });
    case 'retroItem':
      // The item belongs to the old project; its Features list is the nearest screen.
      return routePath('retroFeatures', { key });
    case 'workItem':
    case 'workItemAttachment':
    case 'featureBoard':
      return routePath('storiesBacklog', { key });
    default:
      // The open item belongs to the old project, so it is dropped.
      return routePath(route.id, { ...route.params, key } as RouteParams[RouteId], {
        ...route.query,
        item: undefined,
      });
  }
}
