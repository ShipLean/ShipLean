import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';
import type { ProjectSettingsDto } from '../../../api/projects';
import { projectSettingsKey } from '../settingsApi';

const handlers: [string, () => void][] = [];
vi.mock('../../../realtime', () => ({
  registerInvalidation: (type: string, handler: () => void) => {
    handlers.push([type, handler]);
    return () => {};
  },
}));

const { SettingsStates } = await import('./SettingsStates');
const { SettingsDraftContext } = await import('../draft/LoadedSettings');
const { toDraft } = await import('../draft/model');

const settings: ProjectSettingsDto = {
  general: { key: 'SHIP', name: 'Storefront', description: '', workingDays: [1] },
  members: [],
  sprints: { velocityWindow: 3, velocityOverride: null },
  states: [],
};

test.each([
  'statesChanged',
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemRemoved',
])('statesTab_When%sArrives_ShouldReadSettingsAgain', (event) => {
  handlers.length = 0;
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const spy = vi.spyOn(client, 'invalidateQueries');
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <SettingsDraftContext.Provider
          value={{
            settings,
            draft: toDraft(settings),
            setDraft: () => {},
            readOnly: false,
            errors: [],
            focus: undefined,
          }}
        >
          <SettingsStates projectId="p1" />
        </SettingsDraftContext.Provider>
      </QueryClientProvider>,
    ),
  );

  const handler = handlers.find(([type]) => type === event)?.[1];
  expect(handler).toBeDefined();
  handler?.();
  expect(spy).toHaveBeenCalledWith({ queryKey: projectSettingsKey('p1') });
});
