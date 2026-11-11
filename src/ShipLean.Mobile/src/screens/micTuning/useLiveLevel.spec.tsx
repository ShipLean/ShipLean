import { act, renderHook } from '@testing-library/react-native';
import { installFakeEngines } from '../../test-support/voiceUi';
import { useLiveLevel } from './useLiveLevel';

// Outside a navigator there is no focus to follow: the effect runs on mount and cleans up on unmount.
jest.mock('expo-router', () => ({
  ...jest.requireActual<object>('expo-router'),
  useFocusEffect: (effect: () => (() => void) | undefined) => {
    jest.requireActual<typeof import('react')>('react').useEffect(effect, [effect]);
  },
}));

describe('useLiveLevel', () => {
  it('returns the level of the watched stream while active, and stops watching on unmount', async () => {
    const { recognizer } = installFakeEngines();

    const hook = await renderHook(() => useLiveLevel(true));
    expect(recognizer.watchers.size).toBe(1);
    await act(async () => recognizer.emitLevel(0.5));

    expect(hook.result.current).toEqual({ level: 0.5, failed: false });
    expect(recognizer.starts).toEqual([]);

    await hook.unmount();
    expect(recognizer.watchers.size).toBe(0);
  });

  it('watches nothing while inactive', async () => {
    const { recognizer } = installFakeEngines();

    const hook = await renderHook(() => useLiveLevel(false));
    await act(async () => {});

    expect(hook.result.current).toEqual({ level: 0, failed: false });
    expect(recognizer.watchers.size).toBe(0);
  });

  it('is failed when the stream fails, and clears when active turns on again', async () => {
    const { recognizer } = installFakeEngines();

    const hook = await renderHook<ReturnType<typeof useLiveLevel>, { on: boolean }>(
      ({ on }) => useLiveLevel(on),
      { initialProps: { on: true } },
    );
    await act(async () => recognizer.failWatch());
    expect(hook.result.current.failed).toBe(true);

    await hook.rerender({ on: false });
    expect(hook.result.current.failed).toBe(false);
    expect(recognizer.watchers.size).toBe(0);
    await hook.rerender({ on: true });
    expect(hook.result.current.failed).toBe(false);
  });
});
