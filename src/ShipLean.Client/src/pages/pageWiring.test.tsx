import { isValidElement, type ReactElement } from 'react';
import { expect, test, vi } from 'vitest';
import { InstanceGeneralSlot } from '../slots/InstanceGeneralSlot';
import { SettingsIntegrationsSlot } from '../slots/SettingsIntegrationsSlot';
import { SettingsStatesSlot } from '../slots/SettingsStatesSlot';
import { DiscussionSlot } from '../slots/DiscussionSlot';
import { LinksSlot } from '../slots/LinksSlot';
import { SideSlot } from '../slots/SideSlot';
import { TasksSlot } from '../slots/TasksSlot';
import { backlogSlots } from './backlogSlots';
import { boardSlots } from './boardSlots';
import { capacitySlots } from './capacitySlots';
import { importSlots } from './importSlots';
import { instanceSettingsSlots } from './instanceSettingsSlots';
import { backlogRouteIds, boardRouteIds, pageRouteIds } from './pageRouteIds';
import { projectSettingsSlots } from './projectSettingsSlots';
import { sprintBacklogSlots } from './sprintBacklogSlots';
import { sprintsSlots } from './sprintsSlots';
import { taskboardSlots } from './taskboardSlots';
import { attachmentSlots, workItemSlots } from './workItemSlots';
import { projectDto, sprintDto } from './pagesFixtures';

const project = projectDto('SHIP');
const sprint = sprintDto('sprint-24');
const sprints = [sprint];
const loaded = { data: sprints, isError: false };

function element(node: unknown): ReactElement<Record<string, unknown>> {
  if (!isValidElement<Record<string, unknown>>(node)) throw new Error('expected an element');
  return node;
}

test('boardSlots_WhenSettingsInQuery_ShouldPassSectionAndTheProject', () => {
  const slots = boardSlots(project, 'features', new URLSearchParams('settings=columns'), vi.fn());
  expect(slots.board).toEqual({ project, level: 'features' });
  expect(slots.boardSettings).toMatchObject({ project, level: 'features', section: 'columns' });
});

test('boardSlots_WhenSettingsAbsent_ShouldPassNoSection', () => {
  const slots = boardSlots(project, 'stories', new URLSearchParams('other=1'), vi.fn());
  expect(slots.boardSettings.section).toBeUndefined();
});

test('boardSlots_WhenSectionChangedAndClosed_ShouldSetAndRemoveTheQueryKeepingTheRest', () => {
  const setParams = vi.fn();
  const slots = boardSlots(
    project,
    'stories',
    new URLSearchParams('settings=columns&x=1'),
    setParams,
  );

  slots.boardSettings.onSectionChange('swimlanes');
  const [changed, changedOptions] = setParams.mock.calls[0] as [URLSearchParams, unknown];
  expect(changed.toString()).toBe('settings=swimlanes&x=1');
  expect(changedOptions).toEqual({ replace: true });

  slots.boardSettings.onClose();
  const [closed, closedOptions] = setParams.mock.calls[1] as [URLSearchParams, unknown];
  expect(closed.toString()).toBe('x=1');
  expect(closedOptions).toBeUndefined();
});

test('taskboardSlots_WhenSettingsInQuery_ShouldPassSectionAndTheSprint', () => {
  const slots = taskboardSlots(
    project,
    sprint,
    sprints,
    new URLSearchParams('settings=fields'),
    vi.fn(),
  );
  expect(slots.taskboardSettings).toMatchObject({ project, sprint, sprints, section: 'fields' });
  expect(slots.taskboard).toEqual({ project, sprint, sprints });
  expect(slots.header).toEqual({ variant: 'slim', project, sprint, sprints, tab: 'taskboard' });
});

test('taskboardSlots_WhenClosed_ShouldRemoveTheSettingsQuery', () => {
  const setParams = vi.fn();
  const slots = taskboardSlots(
    project,
    sprint,
    sprints,
    new URLSearchParams('settings=fields&a=b'),
    setParams,
  );
  slots.taskboardSettings.onClose();
  const [closed] = setParams.mock.calls[0] as [URLSearchParams];
  expect(closed.toString()).toBe('a=b');
});

test('sprintPages_ShouldPassTheSprintAndTheRightHeader', () => {
  expect(sprintBacklogSlots(project, sprint, sprints)).toEqual({
    header: { variant: 'full', project, sprint, sprints, tab: 'backlog' },
    sprintBacklog: { project, sprint, sprints },
  });
  expect(capacitySlots(project, sprint, sprints)).toEqual({
    header: { variant: 'full', project, sprint, sprints, tab: 'capacity' },
    capacity: { project, sprint, sprints },
  });
  expect(sprintsSlots(project, sprints)).toEqual({
    sprintEndPrompt: { project, sprints },
    sprintsList: { project, sprints },
    newSprintRail: { project, sprints },
  });
});

test('backlogSlots_WhenStoriesInEachMode_ShouldMountOnlyThatModesSlot', () => {
  const props = (mode: 'list' | 'planning' | 'tree') => ({ project, level: 'stories', mode });
  const list = backlogSlots(project, 'stories', 'list', loaded);
  expect(list.listMode).toEqual(props('list'));
  expect([list.planningMode, list.treeMode, list.levelBacklog]).toEqual([
    undefined,
    undefined,
    undefined,
  ]);
  const planning = backlogSlots(project, 'stories', 'planning', loaded);
  expect(planning.planningMode).toEqual(props('planning'));
  expect([planning.listMode, planning.treeMode]).toEqual([undefined, undefined]);
  const tree = backlogSlots(project, 'stories', 'tree', loaded);
  expect(tree.treeMode).toEqual(props('tree'));
  expect([tree.listMode, tree.planningMode]).toEqual([undefined, undefined]);
});

test('backlogSlots_WhenFeaturesOrEpics_ShouldMountTheLevelBacklogOnly', () => {
  for (const level of ['features', 'epics'] as const) {
    const slots = backlogSlots(project, level, 'list', loaded);
    expect(slots.levelBacklog).toEqual({ project, level, mode: 'list' });
    expect([slots.listMode, slots.planningMode, slots.treeMode]).toEqual([
      undefined,
      undefined,
      undefined,
    ]);
  }
});

test('backlogSlots_WhenSprintsPendingOrFailed_ShouldWaitOrGiveNone', () => {
  expect(
    backlogSlots(project, 'stories', 'list', { data: undefined, isError: false }).sprintEndPrompt,
  ).toBeUndefined();
  const failed = backlogSlots(project, 'stories', 'list', { data: undefined, isError: true });
  expect(failed.sprintEndPrompt).toEqual({ project, sprints: [] });
  expect(failed.listMode).toBeDefined();
  expect(backlogSlots(project, 'stories', 'list', loaded).sprintEndPrompt).toEqual({
    project,
    sprints,
  });
});

test('projectSettingsSlots_ShouldFrameEveryTabAndHandOverTheTwoContents', () => {
  for (const tab of ['general', 'states', 'members', 'sprints', 'integrations'] as const) {
    const { settingsDraft } = projectSettingsSlots(project, tab);
    expect(settingsDraft).toMatchObject({ project, tab });
    const states = element(settingsDraft.states);
    expect(states.type).toBe(SettingsStatesSlot);
    expect(states.props).toEqual({ project, tab });
    const integrations = element(settingsDraft.integrations);
    expect(integrations.type).toBe(SettingsIntegrationsSlot);
    expect(integrations.props).toEqual({ project, tab });
  }
});

test('instanceSettingsSlots_ShouldMountTheSlotOfTheTabOnly', () => {
  expect(instanceSettingsSlots('general')).toEqual({
    instanceGeneral: { tab: 'general' },
    instanceUsers: undefined,
    instanceProjects: undefined,
    instanceIntegrations: undefined,
    instanceAgents: undefined,
  });
  expect(instanceSettingsSlots('users')).toEqual({
    instanceGeneral: undefined,
    instanceUsers: { tab: 'users' },
    instanceProjects: undefined,
    instanceIntegrations: undefined,
    instanceAgents: undefined,
  });
  expect(instanceSettingsSlots('projects')).toEqual({
    instanceGeneral: undefined,
    instanceUsers: undefined,
    instanceProjects: { tab: 'projects' },
    instanceIntegrations: undefined,
    instanceAgents: undefined,
  });
  expect(instanceSettingsSlots('integrations')).toEqual({
    instanceGeneral: undefined,
    instanceUsers: undefined,
    instanceProjects: undefined,
    instanceIntegrations: { tab: 'integrations' },
    instanceAgents: undefined,
  });
  expect(instanceSettingsSlots('agents')).toEqual({
    instanceGeneral: undefined,
    instanceUsers: undefined,
    instanceProjects: undefined,
    instanceIntegrations: undefined,
    instanceAgents: { tab: 'agents' },
  });
  expect(InstanceGeneralSlot).toBeDefined();
});

test('workItemSlots_ShouldHandTheFormTheFourOtherSlots', () => {
  const { form } = workItemSlots(project, 'SHIP-141');
  expect(form).toMatchObject({ project, itemKey: 'SHIP-141' });
  const given = { links: LinksSlot, tasks: TasksSlot, discussion: DiscussionSlot, side: SideSlot };
  for (const [name, slot] of Object.entries(given)) {
    const node = element(form[name as keyof typeof given]);
    expect(node.type).toBe(slot);
    expect(node.props).toEqual({ project, itemKey: 'SHIP-141' });
  }
});

test('attachmentSlots_ShouldPassTheProjectItemAndAttachment', () => {
  expect(attachmentSlots(project, 'SHIP-101', 'a1')).toEqual({
    attachment: { project, itemKey: 'SHIP-101', attachmentId: 'a1' },
  });
});

test('importSlots_ShouldPassTheProjectOrNull', () => {
  expect(importSlots(project)).toEqual({ importStepper: { project }, importRun: { project } });
  expect(importSlots(null)).toEqual({
    importStepper: { project: null },
    importRun: { project: null },
  });
});

test('pageRouteIds_ShouldNameTheScreenEachPageReports', () => {
  expect(backlogRouteIds).toEqual({
    stories: 'storiesBacklog',
    features: 'featuresBacklog',
    epics: 'epicsBacklog',
  });
  expect(boardRouteIds).toEqual({
    stories: 'storiesBoard',
    features: 'featuresBoard',
    epics: 'epicsBoard',
  });
  expect(pageRouteIds).toEqual({
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
  });
});
