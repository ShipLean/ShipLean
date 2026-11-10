import { act, screen } from '@testing-library/react-native';
import { AppState, type AppStateStatus } from 'react-native';
import { kvStore } from '../../session/session';
import { renderScreen } from '../../test-support/renderScreen';
import { installFakeEngines } from '../../test-support/voiceUi';
import { primitiveIds } from '../../ui/testIds';
import { ToastHost } from '../../ui/Toast';
import { writeLastMic, writeLastRoute } from './lastPlace';
import { MicPermissionWatcher } from './MicPermissionWatcher';

let mockPath = '/';
let mockNavKey: string | undefined = 'root';
const mockReplace = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  usePathname: () => mockPath,
  useRootNavigationState: () => ({ key: mockNavKey }),
  router: { replace: (to: string) => mockReplace(to) },
}));

const listeners: ((s: AppStateStatus) => void)[] = [];

beforeEach(() => {
  jest.useFakeTimers();
  mockPath = '/';
  mockNavKey = 'root';
  mockReplace.mockClear();
  listeners.length = 0;
  jest.spyOn(AppState, 'addEventListener').mockImplementation((_type, cb) => {
    listeners.push(cb as (s: AppStateStatus) => void);
    return { remove: () => {} };
  });
});

afterEach(() => {
  jest.useRealTimers();
});

const mount = () =>
  renderScreen(
    <ToastHost>
      <MicPermissionWatcher />
    </ToastHost>,
  );
const foreground = () => act(async () => listeners.forEach((cb) => cb('active')));
const toastGone = () => act(async () => jest.advanceTimersByTime(10_000));

describe('MicPermissionWatcher', () => {
  it('returning from Settings with the mic on shows the toast once', async () => {
    const { mic } = installFakeEngines();
    mic.set('deniedFinal');
    await mount();
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();

    mic.set('granted');
    await foreground();
    expect(screen.getByTestId(primitiveIds.toast)).toHaveTextContent(
      'Microphone on · hold to talk',
    );
    expect(screen.queryByTestId(primitiveIds.toastAction)).toBeNull();
    expect(mockReplace).not.toHaveBeenCalled();

    await toastGone();
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
    await foreground();
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
  });

  it('a normal foreground with no change shows nothing', async () => {
    installFakeEngines();
    await mount();
    await foreground();
    await foreground();
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('stays quiet while the mic is still refused', async () => {
    const { mic } = installFakeEngines();
    mic.set('deniedFinal');
    await mount();
    await foreground();
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
  });

  it('a cold start after the permission changed shows the toast and returns to the stored route', async () => {
    const { mic } = installFakeEngines();
    writeLastMic(kvStore, 'deniedFinal');
    writeLastRoute(kvStore, '/p/SHIP/inbox');
    mic.set('granted');
    mockPath = '/';
    await mount();
    expect(await screen.findByTestId(primitiveIds.toast)).toHaveTextContent(
      'Microphone on · hold to talk',
    );
    expect(mockReplace).toHaveBeenCalledWith('/p/SHIP/inbox');
  });

  it('a cold start with nothing changed does not navigate', async () => {
    installFakeEngines();
    writeLastMic(kvStore, 'granted');
    writeLastRoute(kvStore, '/p/SHIP/inbox');
    await mount();
    await foreground();
    expect(mockReplace).not.toHaveBeenCalled();
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
  });

  it('keeps the current route so a later cold start can return to it', async () => {
    installFakeEngines();
    mockPath = '/p/SHIP/backlog';
    await mount();
    expect(kvStore.getItem('voice.mic.lastRoute')).toBe('/p/SHIP/backlog');
  });
});
