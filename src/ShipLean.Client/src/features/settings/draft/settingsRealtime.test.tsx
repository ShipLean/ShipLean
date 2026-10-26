import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import { projectSettingsKey } from '../settingsApi';

const handlers: [string, () => void][] = [];
vi.mock('../../../realtime', () => ({
  registerInvalidation: (type: string, handler: () => void) => {
    handlers.push([type, handler]);
    return () => {};
  },
}));

const { SettingsDraftFrame } = await import('./SettingsDraftFrame');

const project = { id: 'p1', key: 'SHIP', name: 'Storefront', role: 'admin' } as ProjectDto;

test('settingsFrame_WhenProjectChangedByTeammate_ShouldReadSettingsAgain', () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const spy = vi.spyOn(client, 'invalidateQueries');
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <SettingsDraftFrame project={project} tab="general" states={null} integrations={null} />
      </QueryClientProvider>,
    ),
  );

  const handler = handlers.find(([type]) => type === 'projectChanged')?.[1];
  expect(handler).toBeDefined();
  handler?.();
  expect(spy).toHaveBeenCalledWith({ queryKey: projectSettingsKey('p1') });
});
