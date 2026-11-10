import { fireEvent, screen } from '@testing-library/react-native';
import { Platform } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { installFakeEngines } from '../test-support/voiceUi';
import { FakeModelStore } from '../test-support/voice';
import { MicrophoneStep } from './MicrophoneStep';
import { microphoneStepIds } from './MicrophoneStep.testIds';

jest.mock('../voice/manifest', () => {
  const actual = jest.requireActual<typeof import('../voice/manifest')>('../voice/manifest');
  const sizes = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
  return {
    ...actual,
    MODEL_FILES: actual.MODEL_FILES.map((f) => ({ ...f, bytes: sizes[f.model] })),
  };
});

describe('MicrophoneStep', () => {
  it('Continue asks once; Not now never asks', async () => {
    const { mic } = installFakeEngines();
    const onContinue = jest.fn();
    const onNotNow = jest.fn();
    const first = await renderScreen(
      <MicrophoneStep onContinue={onContinue} onNotNow={onNotNow} />,
    );

    await fireEvent.press(screen.getByTestId(microphoneStepIds.continue));

    expect(mic.requests).toBe(1);
    expect(onContinue).toHaveBeenCalledTimes(1);
    expect(onNotNow).not.toHaveBeenCalled();
    await first.unmount();

    const second = installFakeEngines();
    const later = jest.fn();
    await renderScreen(<MicrophoneStep onContinue={jest.fn()} onNotNow={later} />);

    await fireEvent.press(screen.getByTestId(microphoneStepIds.notNow));

    expect(second.mic.requests).toBe(0);
    expect(later).toHaveBeenCalledTimes(1);
  });

  it('carries on to Downloading after a refusal too', async () => {
    const { mic } = installFakeEngines();
    mic.answerRequests(false);
    const onContinue = jest.fn();
    await renderScreen(<MicrophoneStep onContinue={onContinue} onNotNow={jest.fn()} />);

    await fireEvent.press(screen.getByTestId(microphoneStepIds.continue));

    expect(mic.requests).toBe(1);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it('says nothing you say is kept', async () => {
    installFakeEngines();
    await renderScreen(<MicrophoneStep onContinue={jest.fn()} onNotNow={jest.fn()} />);

    expect(screen.getByTestId(microphoneStepIds.privacy)).toHaveTextContent(
      /Nothing you say is kept; only the text you approve is posted/,
    );
    expect(screen.queryByText(/30 days/)).toBeNull();
    expect(screen.queryByText(/keep their clip/i)).toBeNull();
  });

  it('shows the running download as a thin line, and none when nothing downloads', async () => {
    const models = new FakeModelStore();
    models.set({ phase: 'downloading', bytesDone: 124_000_000, bytesTotal: 3_100_000_000 });
    installFakeEngines({ models });
    const first = await renderScreen(
      <MicrophoneStep onContinue={jest.fn()} onNotNow={jest.fn()} />,
    );
    expect(screen.getByTestId(microphoneStepIds.downloadLine)).toHaveTextContent(
      'Voice models downloading · 4%',
    );
    await first.unmount();

    models.set({ phase: 'skipped' });
    await renderScreen(<MicrophoneStep onContinue={jest.fn()} onNotNow={jest.fn()} />);
    expect(screen.queryByTestId(microphoneStepIds.downloadLine)).toBeNull();
  });

  it.each([
    ['ios', 'Settings.'],
    ['android', "the app's settings."],
  ] as const)('on %s says where to change it later', async (os, where) => {
    const platform = jest.replaceProperty(Platform, 'OS', os);
    installFakeEngines();
    await renderScreen(<MicrophoneStep onContinue={jest.fn()} onNotNow={jest.fn()} />);
    expect(screen.getByTestId(microphoneStepIds.next)).toHaveTextContent(
      new RegExp(`change it any time in ${where.replace('.', '\\.')}$`),
    );
    platform.restore();
  });
});
