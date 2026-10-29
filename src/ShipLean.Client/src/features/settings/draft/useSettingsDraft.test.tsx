import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';
import type { ProjectSettingsDto, ProjectSettingsPut } from '../../../api/projects';
import { WORK_ITEM_TYPE_IDS } from '../../../api/refs';

const puts: ProjectSettingsPut[] = [];
vi.mock('../settingsApi', () => ({
  projectSettingsKey: (id: string) => ['projects', id, 'settings'],
  saveProjectSettings: (_id: string, put: ProjectSettingsPut) => {
    puts.push(put);
    return Promise.resolve(teammate);
  },
}));

const { useSettingsDraft } = await import('./useSettingsDraft');

const user = (id: string) => ({
  id,
  name: id,
  initials: 'X',
  avatarColour: '#000',
  photoUrl: null,
});
const loaded: ProjectSettingsDto = {
  general: {
    key: 'SHIP',
    name: 'Storefront',
    description: '',
    workingDays: [1, 2, 3, 4, 5],
    agentConnectionId: null,
    repoUrl: null,
    repoBaseBranch: null,
    holidayCountry: null,
    agentConnections: [],
  },
  members: [{ user: user('joel'), email: 'joel@x', role: 'admin', status: 'active' }],
  sprints: { velocityWindow: 3, velocityOverride: null, sprintNaming: 'number' },
  states: WORK_ITEM_TYPE_IDS.flatMap((type) => [
    {
      id: `${type}-new`,
      type,
      name: 'New',
      category: 'proposed' as const,
      order: 1,
      itemCount: 0,
    },
    {
      id: `${type}-done`,
      type,
      name: 'Done',
      category: 'completed' as const,
      order: 2,
      itemCount: 0,
    },
  ]),
};
const teammate: ProjectSettingsDto = {
  ...loaded,
  general: { ...loaded.general, workingDays: [1, 2, 3, 4, 5, 6] },
  members: [
    ...loaded.members,
    { user: user('lee'), email: 'lee@x', role: 'member', status: 'active' },
  ],
};

const seen: { hook?: ReturnType<typeof useSettingsDraft> } = {};
function Probe({ settings }: { settings: ProjectSettingsDto }) {
  const current = useSettingsDraft('p1', settings);
  useEffect(() => {
    seen.hook = current;
  });
  return null;
}

test('save_WhenTeammateSavedWhileDirty_ShouldKeepTheirChangesAndSendMine', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient();
  const root = createRoot(document.createElement('div'));
  const render = (settings: ProjectSettingsDto) =>
    act(() =>
      root.render(
        <QueryClientProvider client={client}>
          <Probe settings={settings} />
        </QueryClientProvider>,
      ),
    );
  render(loaded);
  act(() => seen.hook?.setDraft((d) => ({ ...d, name: 'Storefront Plus' })));
  render(teammate);
  expect(seen.hook?.stale).toBe(true);
  expect(seen.hook?.changes.map((c) => c.label)).toEqual(['Name']);
  await act(async () => {
    await seen.hook?.save();
  });
  expect(puts).toHaveLength(1);
  expect(puts[0].general.name).toBe('Storefront Plus');
  expect(puts[0].members.map((m) => m.email)).toEqual(['joel@x', 'lee@x']);
  expect(puts[0].general.workingDays).toEqual([1, 2, 3, 4, 5, 6]);
});

test('rebase_WhenOnlyItemCountsChangedWhileDirty_ShouldTakeTheCountsWithoutTheStaleNotice', () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient();
  const root = createRoot(document.createElement('div'));
  const render = (settings: ProjectSettingsDto) =>
    act(() =>
      root.render(
        <QueryClientProvider client={client}>
          <Probe settings={settings} />
        </QueryClientProvider>,
      ),
    );
  render(loaded);
  act(() => seen.hook?.setDraft((d) => ({ ...d, name: 'Storefront Plus' })));
  render({
    ...loaded,
    states: loaded.states.map((s) => ({ ...s, itemCount: 9 })),
  });
  expect(seen.hook?.stale).toBe(false);
  expect(seen.hook?.draft.name).toBe('Storefront Plus');
});
