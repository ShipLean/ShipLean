import type { Level, RouteId } from '../routes';

// The route id each page reports its presence under (the screen the caller is on, shown to the others).
export const backlogRouteIds: Record<Level, RouteId> = {
  stories: 'storiesBacklog',
  features: 'featuresBacklog',
  epics: 'epicsBacklog',
};

export const boardRouteIds: Record<Level, RouteId> = {
  stories: 'storiesBoard',
  features: 'featuresBoard',
  epics: 'epicsBoard',
};

// Every other page has one route id.
export const pageRouteIds = {
  workItem: 'workItem',
  workItemAttachment: 'workItemAttachment',
  featureBoard: 'featureBoard',
  sprints: 'sprints',
  sprintBacklog: 'sprintBacklog',
  taskboard: 'taskboard',
  capacity: 'capacity',
  importIntoProject: 'jiraImport',
  importWithoutProject: 'importStart',
  projectSettings: 'projectSettings',
  newProject: 'newProject',
  instanceSettings: 'instanceSettings',
  profile: 'profile',
} as const satisfies Record<string, RouteId>;
