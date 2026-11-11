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
  it('returns the level of a session while active, and cancels it on unmount', async () => {
    const { recognizer } = installFakeEngines();
    recognizer.script([], '', [0.25, 0.5]);

    const hook = await renderHook(() => useLiveLevel(true));
    await act(async () => {});

    expect(hook.result.current).toEqual({ level: 0.5, failed: false });
    expect(recognizer.starts).toHaveLength(1);

    await hook.unmount();
    expect(recognizer.cancelled).toBe(1);
  });

  it('starts nothing while inactive', async () => {
    const { recognizer } = installFakeEngines();

    const hook = await renderHook(() => useLiveLevel(false));
    await act(async () => {});

    expect(hook.result.current).toEqual({ level: 0, failed: false });
    expect(recognizer.starts).toEqual([]);
  });

  it('is failed when start rejects, and clears when active turns on again', async () => {
    const { recognizer } = installFakeEngines();
    const start = jest.spyOn(recognizer, 'start').mockRejectedValueOnce(new Error('no model'));

    const hook = await renderHook<ReturnType<typeof useLiveLevel>, { on: boolean }>(
      ({ on }) => useLiveLevel(on),
      {
        initialProps: { on: true },
      },
    );
    await act(async () => {});
    expect(hook.result.current.failed).toBe(true);

    await hook.rerender({ on: false });
    expect(hook.result.current.failed).toBe(false);
    start.mockRestore();
    await hook.rerender({ on: true });
    await act(async () => {});
    expect(hook.result.current.failed).toBe(false);
  });
});
