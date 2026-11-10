import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { Platform } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { FakeModelStore } from '../test-support/voice';
import { installFakeEngines } from '../test-support/voiceUi';
import type { ModelStatus } from '../voice/contracts';
import { MODEL_FILES } from '../voice/manifest';
import { micPermissionSheetId } from '../voice/sheets/MicPermissionSheet.testIds';
import { VoiceSettings } from './VoiceSettings';
import { storageRowId, voiceSettingsIds } from './VoiceSettings.testIds';

// The manifest's sizes are 0 until they are pinned: sized entries, Parakeet split over two files as it may be.
jest.mock('../voice/manifest', () => {
  const actual = jest.requireActual<typeof import('../voice/manifest')>('../voice/manifest');
  const sizes = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
  const parakeetFiles = actual.MODEL_FILES.filter((f) => f.model === 'parakeet').length;
  return {
    ...actual,
    MODEL_FILES: actual.MODEL_FILES.flatMap((f) =>
      f.model === 'parakeet'
        ? [
            { ...f, id: `${f.id}-a`, bytes: sizes.parakeet / 3 / parakeetFiles },
            { ...f, id: `${f.id}-b`, bytes: (sizes.parakeet * 2) / 3 / parakeetFiles },
          ]
        : [{ ...f, bytes: sizes[f.model] }],
    ),
  };
});

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual<typeof import('expo-router')>('expo-router'),
  useRouter: () => ({ push: mockPush, back: jest.fn(), canGoBack: () => false }),
}));

const idsOf = (model: string) => MODEL_FILES.filter((f) => f.model === model).map((f) => f.id);
const sizeOf = (model: string) =>
  MODEL_FILES.filter((f) => f.model === model).reduce((sum, f) => sum + f.bytes, 0);
const checked = (model: string) =>
  MODEL_FILES.filter((f) => f.model === model).map((f) => ({
    id: f.id,
    state: 'checked' as const,
    bytes: f.bytes,
  }));

// Ready on Auto, which is 4B on this 12 GB phone.
function ready(extra: Partial<ModelStatus> = {}): Partial<ModelStatus> {
  return {
    phase: 'ready',
    choice: 'auto',
    resolved: 'qwen3-4b',
    files: [...checked('parakeet'), ...checked('qwen3-4b')],
    ...extra,
  };
}

// A store whose deleteAll does what the real one does: the files go, voice turns off, the choice stays.
class DeletingStore extends FakeModelStore {
  override async deleteAll(): Promise<void> {
    await super.deleteAll();
    this.set({ phase: 'none', resolved: null, files: [], bytesDone: 0, bytesTotal: 0 });
  }
}

async function render(set: Partial<ModelStatus>, models: FakeModelStore = new FakeModelStore()) {
  models.setMemory({ reportedGb: 11.2, soldGb: 12 });
  models.set(set);
  const engines = installFakeEngines({ models });
  await renderScreen(<VoiceSettings />);
  // The microphone's state arrives from the phone after the first render.
  await waitFor(() =>
    expect(screen.getByTestId(voiceSettingsIds.microphoneValue)).toBeOnTheScreen(),
  );
  return engines;
}

describe('VoiceSettings', () => {
  it('shows the card, the models, the storage per model and the licences row', async () => {
    await render(ready());

    expect(screen.getByTestId(voiceSettingsIds.status)).toHaveTextContent(
      /Voice is ready · Pixel 9 · 12 GB memory/,
    );
    expect(screen.getByTestId(voiceSettingsIds.commandModel)).toHaveTextContent(/Auto · Qwen3 4B/);
    expect(screen.getByTestId(voiceSettingsIds.speech)).toHaveTextContent(/Parakeet TDT 0.6B/);
    expect(screen.getByTestId(voiceSettingsIds.tryOther)).toHaveTextContent(/Later/);
    expect(screen.getByTestId(storageRowId('parakeet'))).toHaveTextContent(/0\.6 GB/);
    expect(screen.getByTestId(storageRowId('qwen3-4b'))).toHaveTextContent(/2\.5 GB/);
    expect(screen.getByTestId(voiceSettingsIds.storageTotal)).toHaveTextContent(/3\.1 GB/);
    expect(screen.getByTestId(voiceSettingsIds.licences)).toHaveTextContent(
      'Parakeet TDT 0.6B (CC-BY-4.0) · Qwen3 (Apache 2.0)',
      { exact: false },
    );
  });

  it('Wi-Fi only calls the store, and Set up opens the model screen when voice is off', async () => {
    const { models } = await render({ phase: 'skipped', resolved: null, files: [] });

    await fireEvent(screen.getByTestId(voiceSettingsIds.wifiOnly), 'valueChange', false);
    await fireEvent.press(screen.getByTestId(voiceSettingsIds.setUp));

    expect((models as FakeModelStore).calls).toContainEqual({ method: 'setWifiOnly', on: false });
    expect(mockPush).toHaveBeenCalledWith({
      pathname: '/setup/[step]',
      params: { step: 'models' },
    });
  });

  it('Delete models confirms, calls deleteAll, and the choices stay shown', async () => {
    const models = new DeletingStore();
    await render(
      ready({
        choice: 'qwen3-1.7b',
        resolved: 'qwen3-1.7b',
        files: [...checked('parakeet'), ...checked('qwen3-1.7b')],
      }),
      models,
    );

    await fireEvent.press(screen.getByTestId(voiceSettingsIds.deleteModels));
    expect(screen.getByTestId(voiceSettingsIds.deleteLine)).toHaveTextContent(
      'Frees 1.7 GB. Voice turns off; your choices are kept.',
      { exact: false },
    );
    expect(models.calls).not.toContainEqual({ method: 'deleteAll' });
    await fireEvent.press(screen.getByTestId(voiceSettingsIds.deleteConfirm));

    expect(models.calls).toContainEqual({ method: 'deleteAll' });
    await waitFor(() =>
      expect(screen.getByTestId(voiceSettingsIds.status)).toHaveTextContent(/Voice is off/),
    );
    expect(screen.getByTestId(voiceSettingsIds.commandModel)).toHaveTextContent(/Qwen3 1\.7B/);
    expect(screen.getByTestId(voiceSettingsIds.setUp)).toBeOnTheScreen();
  });

  it('Cancel on the delete question deletes nothing', async () => {
    const { models } = await render(ready());

    await fireEvent.press(screen.getByTestId(voiceSettingsIds.deleteModels));
    await fireEvent.press(screen.getByTestId(voiceSettingsIds.deleteCancel));

    expect((models as FakeModelStore).calls).not.toContainEqual({ method: 'deleteAll' });
  });

  it('with the microphone off the row reads Off and opens the permission sheet', async () => {
    const { mic } = installFakeEngines();
    mic.set('deniedFinal');
    const models = new FakeModelStore();
    models.setMemory({ reportedGb: 11.2, soldGb: 12 });
    models.set(ready());
    installFakeEngines({ models, mic });
    await renderScreen(<VoiceSettings />);

    await waitFor(() =>
      expect(screen.getByTestId(voiceSettingsIds.microphoneValue)).toHaveTextContent('Off', {
        exact: false,
      }),
    );
    expect(screen.getByTestId(voiceSettingsIds.status)).toHaveTextContent(
      'Voice is ready, but the microphone is off',
      { exact: false },
    );
    expect(screen.getByTestId(voiceSettingsIds.microphone)).toHaveTextContent(
      Platform.OS === 'android'
        ? "Off in the app's settings. Tap to turn it on"
        : 'Off in iPhone Settings. Tap to turn it on',
      { exact: false },
    );
    await fireEvent.press(screen.getByTestId(voiceSettingsIds.microphone));

    expect(screen.getByTestId(micPermissionSheetId)).toBeOnTheScreen();
    expect(mic.settingsOpened).toBe(0);
  });

  it('with the microphone on the row reads On and opens the phone settings', async () => {
    const { mic } = await render(ready());

    expect(screen.getByTestId(voiceSettingsIds.microphoneValue)).toHaveTextContent('On', {
      exact: false,
    });
    await fireEvent.press(screen.getByTestId(voiceSettingsIds.microphone));

    expect(mic.settingsOpened).toBe(1);
    expect(screen.queryByTestId(micPermissionSheetId)).toBeNull();
  });

  it('while a new model downloads, voice still reads ready and the row shows progress', async () => {
    await render(
      ready({
        choice: 'qwen3-1.7b',
        reason: 'wifi',
        bytesTotal: sizeOf('qwen3-1.7b'),
        bytesDone: sizeOf('qwen3-1.7b') * 0.4,
        files: [
          ...checked('parakeet'),
          { id: idsOf('qwen3-1.7b')[0], state: 'downloading', bytes: sizeOf('qwen3-1.7b') * 0.4 },
        ],
      }),
    );

    expect(screen.getByTestId(voiceSettingsIds.status)).toHaveTextContent(/Voice is ready/);
    expect(screen.getByTestId(voiceSettingsIds.commandModel)).toHaveTextContent(/Qwen3 4B/);
    const change = screen.getByTestId(voiceSettingsIds.change);
    expect(change).toHaveTextContent('Downloading Qwen3 1.7B · 40% · waiting for Wi-Fi', {
      exact: false,
    });
    expect(within(change).getByTestId(voiceSettingsIds.changeBar).props.accessibilityValue).toEqual(
      expect.objectContaining({ now: 40 }),
    );
    // The store lists only Parakeet and the target, so the model in use is added from the manifest.
    expect(screen.getByTestId(storageRowId('qwen3-4b'))).toHaveTextContent(/2\.5 GB/);
    expect(screen.getByTestId(voiceSettingsIds.storageTotal)).toHaveTextContent(/3\.5 GB/);
  });

  it('a change that failed its check says so and Try again calls download', async () => {
    const { models } = await render(
      ready({
        choice: 'qwen3-1.7b',
        reason: 'checksum',
        bytesTotal: sizeOf('qwen3-1.7b'),
        bytesDone: sizeOf('qwen3-1.7b'),
        files: [
          ...checked('parakeet'),
          { id: idsOf('qwen3-1.7b')[0], state: 'failed', bytes: sizeOf('qwen3-1.7b') },
        ],
      }),
    );

    expect(screen.getByTestId(voiceSettingsIds.change)).toHaveTextContent('· failed its check', {
      exact: false,
    });
    await fireEvent.press(screen.getByTestId(voiceSettingsIds.changeRetry));

    expect((models as FakeModelStore).calls).toContainEqual({ method: 'download' });
  });

  it('a delete that rejects shows an error line', async () => {
    class FailingStore extends FakeModelStore {
      override deleteAll(): Promise<void> {
        return Promise.reject(new Error('disk'));
      }
    }
    await render(ready(), new FailingStore());

    await fireEvent.press(screen.getByTestId(voiceSettingsIds.deleteModels));
    await fireEvent.press(screen.getByTestId(voiceSettingsIds.deleteConfirm));

    expect(await screen.findByTestId(voiceSettingsIds.deleteError)).toBeOnTheScreen();
  });

  it('Command model opens the change sheet', async () => {
    await render(ready());

    await fireEvent.press(screen.getByTestId(voiceSettingsIds.commandModel));

    expect(await screen.findByTestId('sheet-change-model')).toBeOnTheScreen();
    await act(async () => {});
  });
});
