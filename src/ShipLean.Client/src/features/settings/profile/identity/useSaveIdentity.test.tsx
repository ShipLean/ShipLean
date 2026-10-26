import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, expect, test, vi } from 'vitest';
import type { MeDto } from '../../../../api/me';
import { projectMembersKey, meKey } from '../../../../queries/shared';
import { useSaveIdentity } from './useSaveIdentity';

const settingsApi = vi.hoisted(() => ({
  saveProfile: vi.fn(),
  uploadPhoto: vi.fn(),
  removePhoto: vi.fn(),
}));
vi.mock('../../settingsApi', () => settingsApi);

const me = {
  id: 'me',
  name: 'Priya Shah',
  avatarColour: '#2f7fd8',
  photoUrl: null,
} as MeDto;

afterEach(() => {
  vi.resetAllMocks();
});

function mountSaver() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(meKey, me);
  queryClient.setQueryData(projectMembersKey('p1'), [{ id: 'me' }]);
  const saver: { current?: ReturnType<typeof useSaveIdentity> } = {};
  function Probe() {
    saver.current = useSaveIdentity(me, () => undefined);
    return null;
  }
  const root = createRoot(document.createElement('div'));
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Probe />
      </QueryClientProvider>,
    );
  });
  return { queryClient, saver };
}

test('useSaveIdentity_WhenOnlyColourChanged_ShouldPutTheProfileAndMarkOtherQueriesStale', async () => {
  settingsApi.saveProfile.mockResolvedValue({ ...me, avatarColour: '#7b4fc9' });
  const { queryClient, saver } = mountSaver();

  await act(async () => {
    saver.current!.save({
      name: me.name,
      avatarColour: '#7b4fc9',
      pickedFile: null,
      removePhoto: false,
    });
    await vi.waitFor(() => expect(settingsApi.saveProfile).toHaveBeenCalled());
  });

  expect(settingsApi.saveProfile).toHaveBeenCalledWith({
    name: me.name,
    avatarColour: '#7b4fc9',
  });
  await vi.waitFor(() =>
    expect(queryClient.getQueryState(projectMembersKey('p1'))?.isInvalidated).toBe(true),
  );
});
