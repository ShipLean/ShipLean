import type { RouteId } from '../../../routes';

// Which route ids belong to which side-menu section, so the section stays lit on its sub-pages.
export const backlogsRoutes: readonly RouteId[] = [
  'storiesBacklog',
  'featuresBacklog',
  'epicsBacklog',
  'workItem',
  'workItemAttachment',
];
export const boardsRoutes: readonly RouteId[] = ['storiesBoard', 'featuresBoard', 'epicsBoard'];
export const sprintsRoutes: readonly RouteId[] = [
  'sprints',
  'sprintBacklog',
  'taskboard',
  'capacity',
];
