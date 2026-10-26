import { expect, test } from 'vitest';
import type { ProjectEvent } from '../api/events';
import { createRemoteChanges } from './remoteChanges';

const own = 'tab-1';
const project = '0199a000-0000-7000-8000-000000000012';

const sprintChanged = (clientId: string | null): ProjectEvent => ({
  type: 'sprintChanged',
  projectId: project,
  clientId,
  sprintId: 's1',
});
const importProgress: ProjectEvent = {
  type: 'importProgress',
  projectId: project,
  clientId: own,
  jobId: 'j',
  percent: 10,
  state: 'running',
  message: '',
};
const presenceChanged: ProjectEvent = {
  type: 'presenceChanged',
  projectId: project,
  clientId: own,
  screenId: 'sprints',
  userIds: [],
  sequence: 1,
};

test('remoteChanges_WhenOwnClientId_ShouldSkipExceptImportAndPresence', () => {
  const changes = createRemoteChanges(own);
  const seen: ProjectEvent[] = [];
  changes.onRemote((event) => seen.push(event));
  const byType: ProjectEvent[] = [];
  changes.registerInvalidation('sprintChanged', (event) => byType.push(event));

  changes.handle(sprintChanged(own));
  changes.handle(importProgress);
  changes.handle(presenceChanged);
  changes.handle(sprintChanged('tab-2'));
  changes.handle(sprintChanged(null));

  expect(seen.map((e) => e.type)).toEqual([
    'importProgress',
    'presenceChanged',
    'sprintChanged',
    'sprintChanged',
  ]);
  expect(byType).toHaveLength(2);
});

test('remoteChanges_WhenEventArrives_ShouldCallRegisteredHandlersWithTheEvent', () => {
  const changes = createRemoteChanges(own);
  const sprints: string[] = [];
  const everything: ProjectEvent[] = [];
  changes.registerInvalidation('sprintChanged', (event) => sprints.push(event.sprintId));
  changes.registerInvalidation('projectChanged', () => sprints.push('wrong type'));
  changes.onRemote((event) => everything.push(event));

  const event = sprintChanged('tab-2');
  changes.handle(event);

  expect(sprints).toEqual(['s1']);
  expect(everything).toEqual([event]);
});

test('remoteChanges_WhenUnregistered_ShouldStopCalling', () => {
  const changes = createRemoteChanges(own);
  let typed = 0;
  let all = 0;
  const stopTyped = changes.registerInvalidation('sprintChanged', () => (typed += 1));
  const stopAll = changes.onRemote(() => (all += 1));
  const same = () => (typed += 10);
  const first = changes.registerInvalidation('sprintChanged', same);
  changes.registerInvalidation('sprintChanged', same);

  stopTyped();
  stopAll();
  first();
  changes.handle(sprintChanged('tab-2'));

  expect(all).toBe(0);
  expect(typed).toBe(10);
});
