import { fireEvent, screen } from '@testing-library/react-native';
import { screenIds, screenSetup } from '../screens/testIds';
import { kvValues } from '../test-support/storage';
import { renderApp } from '../test-support/renderApp';
import { renderScreen } from '../test-support/renderScreen';
import { installFakeEngines } from '../test-support/voiceUi';
import { FakeModelStore } from '../test-support/voice';
import { MODEL_FILES } from '../voice/manifest';
import { SETUP_OFFERED_KEY } from './firstRun';
import { formatGb } from './modelChoice';
import { modelsStepIds } from './ModelsStep.testIds';
import { ModelsStep } from './ModelsStep';

// The manifest's sizes are 0 until they are pinned: sized entries, so the totals mean something.
jest.mock('../voice/manifest', () => {
  const actual = jest.requireActual<typeof import('../voice/manifest')>('../voice/manifest');
  const sizes = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
  return {
    ...actual,
    MODEL_FILES: actual.MODEL_FILES.map((f) => ({ ...f, bytes: sizes[f.model] })),
  };
});

const sizeOf = (model: string) =>
  MODEL_FILES.filter((f) => f.model === model).reduce((sum, f) => sum + f.bytes, 0);

function aPhone(memory: { reportedGb: number; soldGb: number }) {
  const models = new FakeModelStore();
  models.set({ phase: 'none', freeBytes: 87_000_000_000 });
  models.setMemory(memory);
  installFakeEngines({ models });
  return models;
}

const TWELVE = { reportedGb: 11.2, soldGb: 12 };
const EIGHT = { reportedGb: 7.6, soldGb: 8 };
const SIX = { reportedGb: 5.6, soldGb: 6 };

beforeEach(() => kvValues.delete(SETUP_OFFERED_KEY));

describe('ModelsStep', () => {
  it('Download and continue chooses Auto, starts the download and opens the microphone step', async () => {
    const models = aPhone(TWELVE);
    await renderApp({ url: '/setup/models' });

    expect(await screen.findByTestId(screenSetup('models'))).toBeOnTheScreen();
    expect(screen.getByTestId(modelsStepIds.optionAuto)).toBeSelected();
    expect(screen.getByTestId(modelsStepIds.autoPick)).toHaveTextContent(
      'This phone: 12 GB → Qwen3 4B',
    );
    expect(screen.getByTestId(modelsStepIds.total)).toHaveTextContent(
      `Download ${formatGb(sizeOf('parakeet') + sizeOf('qwen3-4b'))}`,
    );
    expect(models.calls).toEqual([]);

    await fireEvent.press(screen.getByTestId(modelsStepIds.download));

    expect(models.calls).toEqual([{ method: 'choose', choice: 'auto' }, { method: 'download' }]);
    expect(await screen.findByTestId(screenSetup('microphone'))).toBeOnTheScreen();
  });

  it('Skip voice for now skips and opens Projects', async () => {
    const models = aPhone(TWELVE);
    // Setup was offered (the gate marks it before opening), so Projects doesn't offer it again.
    kvValues.set(SETUP_OFFERED_KEY, '1');
    await renderApp({ url: '/setup/models' });

    await fireEvent.press(await screen.findByTestId(modelsStepIds.skip));

    expect(models.calls).toEqual([{ method: 'skip' }]);
    expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
  });

  it("picking 4B on an 8 GB phone shows the warning; on 6 GB it can't be picked", async () => {
    const eight = aPhone(EIGHT);
    const onContinue = jest.fn();
    const first = await renderScreen(<ModelsStep onContinue={onContinue} onSkip={jest.fn()} />);
    expect(screen.getByTestId(modelsStepIds.autoPick)).toHaveTextContent(/→ Qwen3 1\.7B/);
    expect(screen.getByTestId(modelsStepIds.warning4)).toHaveTextContent(
      'Not recommended with 8 GB: slow, and may be closed when memory runs low.',
    );

    await fireEvent.press(screen.getByTestId(modelsStepIds.option4));
    expect(screen.getByTestId(modelsStepIds.option4)).toBeSelected();
    expect(screen.getByTestId(modelsStepIds.optionAuto)).not.toBeSelected();
    expect(screen.getByTestId(modelsStepIds.total)).toHaveTextContent(
      `Download ${formatGb(sizeOf('parakeet') + sizeOf('qwen3-4b'))}`,
    );
    await fireEvent.press(screen.getByTestId(modelsStepIds.download));
    expect(eight.calls).toEqual([{ method: 'choose', choice: 'qwen3-4b' }, { method: 'download' }]);
    await first.unmount();

    const six = aPhone(SIX);
    await renderScreen(<ModelsStep onContinue={jest.fn()} onSkip={jest.fn()} />);
    expect(screen.queryByTestId(modelsStepIds.warning4)).toBeNull();
    expect(screen.getByTestId(modelsStepIds.blocked4)).toHaveTextContent(
      'Needs 8 GB of memory. This phone has 6 GB.',
    );
    await fireEvent.press(screen.getByTestId(modelsStepIds.option4));
    expect(screen.getByTestId(modelsStepIds.option4)).not.toBeSelected();
    expect(screen.getByTestId(modelsStepIds.optionAuto)).toBeSelected();
    await fireEvent.press(screen.getByTestId(modelsStepIds.download));
    expect(six.calls).toEqual([{ method: 'choose', choice: 'auto' }, { method: 'download' }]);
  });

  it('switches Wi-Fi only through the store', async () => {
    const models = aPhone(TWELVE);
    await renderScreen(<ModelsStep onContinue={jest.fn()} onSkip={jest.fn()} />);
    expect(screen.getByTestId(modelsStepIds.wifiOnly).props.value).toBe(true);

    await fireEvent(screen.getByTestId(modelsStepIds.wifiOnly), 'valueChange', false);

    expect(models.calls).toEqual([{ method: 'setWifiOnly', on: false }]);
  });

  it('offers no voice under 6 GB: the reason, and Continue skips', async () => {
    const models = aPhone({ reportedGb: 3.7, soldGb: 4 });
    const onSkip = jest.fn();
    await renderScreen(<ModelsStep onContinue={jest.fn()} onSkip={onSkip} />);

    expect(screen.getByTestId(modelsStepIds.noVoice)).toHaveTextContent(/has 4 GB/);
    expect(screen.queryByTestId(modelsStepIds.optionAuto)).toBeNull();
    expect(screen.queryByTestId(modelsStepIds.download)).toBeNull();

    await fireEvent.press(screen.getByTestId(modelsStepIds.noVoiceContinue));

    expect(models.calls).toEqual([{ method: 'skip' }]);
    expect(onSkip).toHaveBeenCalledTimes(1);
  });
});
