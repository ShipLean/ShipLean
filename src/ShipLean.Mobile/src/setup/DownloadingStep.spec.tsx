import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { AppState, Linking, Platform, type AppStateStatus } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { installFakeEngines } from '../test-support/voiceUi';
import { FakeModelStore } from '../test-support/voice';
import type { ModelStatus } from '../voice/contracts';
import { MODEL_FILES } from '../voice/manifest';
import { downloadFileRowId, downloadingStepIds } from './DownloadingStep.testIds';
import { DownloadingStep } from './DownloadingStep';
import { formatGb } from './modelChoice';

// The manifest's sizes are 0 until they are pinned: sized entries, Parakeet split over two files as it may be.
jest.mock('../voice/manifest', () => {
  const actual = jest.requireActual<typeof import('../voice/manifest')>('../voice/manifest');
  const sizes = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
  return {
    ...actual,
    MODEL_FILES: actual.MODEL_FILES.flatMap((f) =>
      f.model === 'parakeet'
        ? [
            { ...f, id: `${f.id}-a`, bytes: sizes.parakeet / 3 },
            { ...f, id: `${f.id}-b`, bytes: (sizes.parakeet * 2) / 3 },
          ]
        : [{ ...f, bytes: sizes[f.model] }],
    ),
  };
});

const ids = (model: string) => MODEL_FILES.filter((f) => f.model === model).map((f) => f.id);
const sizeOf = (model: string) =>
  MODEL_FILES.filter((f) => f.model === model).reduce((sum, f) => sum + f.bytes, 0);
const TOTAL = sizeOf('parakeet') + sizeOf('qwen3-4b');

// Parakeet checked, the 4B model partly downloaded.
function downloading(qwenBytes: number): Partial<ModelStatus> {
  return {
    phase: 'downloading',
    resolved: 'qwen3-4b',
    bytesTotal: TOTAL,
    bytesDone: sizeOf('parakeet') + qwenBytes,
    files: [
      ...ids('parakeet').map((id) => ({
        id,
        state: 'checked' as const,
        bytes: MODEL_FILES.find((f) => f.id === id)?.bytes ?? 0,
      })),
      { id: ids('qwen3-4b')[0], state: 'downloading' as const, bytes: qwenBytes },
    ],
  };
}

function render(set: Partial<ModelStatus>, onUseApp = jest.fn()) {
  const models = new FakeModelStore();
  models.set(set);
  const engines = installFakeEngines({ models });
  return {
    ...engines,
    onUseApp,
    done: renderScreen(<DownloadingStep onUseApp={onUseApp} />),
  };
}

describe('DownloadingStep', () => {
  it("shows each file's row, overall progress and time left", async () => {
    const now = jest.spyOn(Date, 'now');
    now.mockReturnValue(1_000_000);
    const { models, done } = render(downloading(600_000_000));
    await done;

    expect(screen.getByTestId(downloadingStepIds.percent)).toHaveTextContent('38%');
    expect(screen.getByTestId(downloadingStepIds.amount)).toHaveTextContent(
      `${((sizeOf('parakeet') + 600_000_000) / 1e9).toFixed(1)} of ${formatGb(TOTAL)}`,
    );
    expect(screen.getByTestId(downloadFileRowId('parakeet'))).toHaveTextContent(
      /Speech to text · downloaded and checked/,
    );
    expect(screen.getByTestId(downloadFileRowId('parakeet'))).toHaveTextContent(
      new RegExp(formatGb(sizeOf('parakeet')).replace('.', '\\.')),
    );
    expect(screen.getByTestId(downloadFileRowId('qwen3-4b'))).toHaveTextContent(
      /Command model · 0\.6 of 2\.5 GB/,
    );
    // No rate is known from one reading.
    expect(screen.getByTestId(downloadingStepIds.amount)).not.toHaveTextContent(/about/);

    // Over the next 20 s 400 MB arrives: 20 MB/s, and the 1.5 GB still to come is about 1 min.
    now.mockReturnValue(1_020_000);
    await act(() => {
      models.set({ bytesDone: sizeOf('parakeet') + 600_000_000 });
    });
    await act(() => {
      now.mockReturnValue(1_040_000);
      models.set(downloading(1_000_000_000));
    });
    expect(screen.getByTestId(downloadingStepIds.amount)).toHaveTextContent(/about 1 min on Wi-Fi/);
    expect(screen.getByTestId(downloadFileRowId('qwen3-4b'))).toHaveTextContent(
      /Command model · 1\.0 of 2\.5 GB/,
    );
    now.mockRestore();
  });

  it('lets the person leave and pause, and says nothing blocks the app', async () => {
    const { models, onUseApp, done } = render(downloading(600_000_000));
    await done;

    await fireEvent.press(screen.getByTestId(downloadingStepIds.useApp));
    expect(onUseApp).toHaveBeenCalledTimes(1);
    expect(models.calls).toEqual([]);

    await fireEvent.press(screen.getByTestId(downloadingStepIds.pause));
    expect(models.calls).toEqual([{ method: 'pause' }]);
    expect(screen.getByTestId(downloadingStepIds.promises)).toHaveTextContent(
      /Carries on if you leave the app or lock the phone/,
    );
  });

  it('asks the iPhone to stay open, and the Android phone nothing', async () => {
    const ios = jest.replaceProperty(Platform, 'OS', 'ios');
    const first = render(downloading(0));
    const view = await first.done;
    expect(screen.getByTestId(downloadingStepIds.keepOpen)).toHaveTextContent(
      'Keep ShipLean open on iPhone to finish faster',
    );
    await view.unmount();
    ios.restore();

    const android = jest.replaceProperty(Platform, 'OS', 'android');
    await render(downloading(0)).done;
    expect(screen.queryByTestId(downloadingStepIds.keepOpen)).toBeNull();
    android.restore();
  });

  it('after a refused microphone shows the Microphone is off card with Open Settings; after Not now the not-asked line', async () => {
    const refused = render(downloading(0));
    refused.mic.set('deniedCanAsk');
    const first = await refused.done;

    expect(await screen.findByTestId(downloadingStepIds.micOff)).toHaveTextContent(
      /Microphone is off/,
    );
    expect(screen.queryByTestId(downloadingStepIds.micNotAsked)).toBeNull();
    await fireEvent.press(screen.getByTestId(downloadingStepIds.micOffSettings));
    expect(refused.mic.settingsOpened).toBe(1);
    // The download goes on.
    expect(refused.models.calls).toEqual([]);
    await first.unmount();

    const notNow = render(downloading(0));
    notNow.mic.set('undetermined');
    await notNow.done;
    expect(await screen.findByTestId(downloadingStepIds.micNotAsked)).toHaveTextContent(
      /Microphone: not asked yet/,
    );
    expect(screen.queryByTestId(downloadingStepIds.micOff)).toBeNull();
    expect(notNow.mic.requests).toBe(0);
  });

  it('shows neither microphone note once it is allowed', async () => {
    const granted = render(downloading(0));
    granted.mic.set('granted');
    await granted.done;
    await screen.findByTestId(downloadingStepIds.files);
    expect(screen.queryByTestId(downloadingStepIds.micOff)).toBeNull();
    expect(screen.queryByTestId(downloadingStepIds.micNotAsked)).toBeNull();
  });

  it('no space shows how much more is needed and offers Qwen3 1.7B when it fits', async () => {
    const stopped: Partial<ModelStatus> = {
      ...downloading(1_700_000_000),
      phase: 'paused',
      reason: 'space',
      freeBytes: 300_000_000,
    };
    const open = jest.spyOn(Linking, 'openSettings').mockResolvedValue();
    const first = render(stopped);
    const view = await first.done;

    const remaining = TOTAL - (sizeOf('parakeet') + 1_700_000_000);
    expect(screen.getByTestId(downloadingStepIds.noSpaceBody)).toHaveTextContent(
      new RegExp(
        `Qwen3 4B needs ${formatGb(remaining)} more, and this phone has ${formatGb(300_000_000)} free\\.`,
      ),
    );
    expect(screen.getByTestId(downloadFileRowId('qwen3-4b'))).toHaveTextContent(
      /Stopped at 1\.7 of 2\.5 GB/,
    );
    expect(screen.getByTestId(downloadingStepIds.useSmaller)).toHaveTextContent(
      'Switch to Qwen3 1.7B',
    );
    expect(screen.queryByTestId(downloadingStepIds.pause)).toBeNull();
    expect(screen.queryByTestId(downloadingStepIds.resume)).toBeNull();

    await fireEvent.press(screen.getByTestId(downloadingStepIds.tryAgain));
    await fireEvent.press(screen.getByTestId(downloadingStepIds.openStorage));
    await fireEvent.press(screen.getByTestId(downloadingStepIds.useSmaller));
    expect(first.models.calls).toEqual([
      { method: 'download' },
      { method: 'choose', choice: 'qwen3-1.7b' },
    ]);
    expect(open).toHaveBeenCalled();
    await view.unmount();

    // Too little room even for the smaller one: it isn't offered.
    const tight = render({
      ...downloading(100_000_000),
      phase: 'paused',
      reason: 'space',
      freeBytes: 10_000_000,
    });
    await tight.done;
    expect(screen.getByTestId(downloadingStepIds.noSpace)).toBeOnTheScreen();
    expect(screen.queryByTestId(downloadingStepIds.useSmaller)).toBeNull();
    expect(screen.queryByTestId(downloadingStepIds.resume)).toBeNull();
  });

  it('offers Resume on a pause that is not for space', async () => {
    const paused = render({ ...downloading(600_000_000), phase: 'paused', reason: 'wifi' });
    await paused.done;
    await fireEvent.press(screen.getByTestId(downloadingStepIds.resume));
    expect(paused.models.calls).toEqual([{ method: 'download' }]);
  });

  it.each(['checksum', 'network'] as const)(
    'a download that failed for %s offers Try again, which downloads again',
    async (reason) => {
      const failed = render({ ...downloading(600_000_000), phase: 'failed', reason });
      await failed.done;
      expect(screen.queryByTestId(downloadingStepIds.noSpace)).toBeNull();
      await fireEvent.press(screen.getByTestId(downloadingStepIds.failedTryAgain));
      expect(failed.models.calls).toEqual([{ method: 'download' }]);
    },
  );

  it('drops the Microphone is off card when the app comes back with the microphone allowed', async () => {
    const listeners: ((s: AppStateStatus) => void)[] = [];
    // Not restored: mockRestore would wipe React Native's own jest mock of AppState.
    jest.spyOn(AppState, 'addEventListener').mockImplementation((_t, cb) => {
      listeners.push(cb as (s: AppStateStatus) => void);
      return { remove: () => {} };
    });
    const back = render(downloading(0));
    back.mic.set('deniedFinal');
    await back.done;
    expect(await screen.findByTestId(downloadingStepIds.micOff)).toBeOnTheScreen();

    // Open Settings, allow the microphone, return to the app.
    back.mic.set('granted');
    await act(async () => listeners.forEach((cb) => cb('active')));
    await waitFor(() => expect(screen.queryByTestId(downloadingStepIds.micOff)).toBeNull());
  });

  it('offers to set up voice when nothing is downloading', async () => {
    const onSetUp = jest.fn();
    const models = new FakeModelStore();
    models.set({ phase: 'skipped' });
    installFakeEngines({ models });
    await renderScreen(<DownloadingStep onUseApp={jest.fn()} onSetUp={onSetUp} />);

    expect(screen.getByTestId(downloadingStepIds.idle)).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(downloadingStepIds.setUp));
    expect(onSetUp).toHaveBeenCalledTimes(1);
  });
});
