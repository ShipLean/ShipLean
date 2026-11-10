import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { FakeModelStore } from '../test-support/voice';
import { installFakeEngines } from '../test-support/voiceUi';
import type { ModelStatus } from '../voice/contracts';
import { MODEL_FILES } from '../voice/manifest';
import { ChangeModelSheet } from './ChangeModelSheet';
import { changeModelIds, changeModelOptionId } from './ChangeModelSheet.testIds';

jest.mock('../voice/manifest', () => {
  const actual = jest.requireActual<typeof import('../voice/manifest')>('../voice/manifest');
  const sizes = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
  return {
    ...actual,
    MODEL_FILES: actual.MODEL_FILES.map((f) => ({ ...f, bytes: sizes[f.model] })),
  };
});

const checked = (model: string) =>
  MODEL_FILES.filter((f) => f.model === model).map((f) => ({
    id: f.id,
    state: 'checked' as const,
    bytes: f.bytes,
  }));

async function render(set: Partial<ModelStatus> = {}, memory = { reportedGb: 11.2, soldGb: 12 }) {
  const models = new FakeModelStore();
  models.setMemory(memory);
  models.set({
    phase: 'ready',
    choice: 'auto',
    resolved: 'qwen3-4b',
    files: [...checked('parakeet'), ...checked('qwen3-4b')],
    ...set,
  });
  const engines = installFakeEngines({ models });
  const onClose = jest.fn();
  await renderScreen(<ChangeModelSheet onClose={onClose} />);
  return { ...engines, onClose };
}

describe('ChangeModelSheet', () => {
  it('picking 1.7B on a 4B phone says 1.1 GB downloads, 4B is kept until checked, and 2.5 GB is freed; Download calls choose', async () => {
    const { models, onClose } = await render();

    await fireEvent.press(screen.getByTestId(changeModelOptionId('qwen3-1.7b')));

    const summary = screen.getByTestId(changeModelIds.summary);
    expect(summary).toHaveTextContent('Qwen3 1.7B downloads 1.1 GB on Wi-Fi.', { exact: false });
    expect(summary).toHaveTextContent('Voice keeps using Qwen3 4B until Qwen3 1.7B is checked.', {
      exact: false,
    });
    expect(summary).toHaveTextContent('Then Qwen3 4B is deleted to free 2.5 GB.', { exact: false });
    expect(screen.getByTestId(changeModelIds.download)).toHaveTextContent('Download 1.1 GB', {
      exact: false,
    });
    expect(models.calls).toEqual([]);
    await fireEvent.press(screen.getByTestId(changeModelIds.download));

    expect(models.calls).toEqual([{ method: 'choose', choice: 'qwen3-1.7b' }]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('Cancel changes nothing', async () => {
    const { models, onClose } = await render();
    await fireEvent.press(screen.getByTestId(changeModelOptionId('qwen3-1.7b')));

    await fireEvent.press(screen.getByTestId(changeModelIds.cancel));

    expect(models.calls).toEqual([]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('the model in use has nothing to download', async () => {
    await render();

    expect(screen.getByTestId(changeModelIds.download)).toBeDisabled();
  });

  it('4B is greyed with its reason on a 6 GB phone', async () => {
    await render(
      { resolved: 'qwen3-1.7b', files: [...checked('parakeet'), ...checked('qwen3-1.7b')] },
      { reportedGb: 5.6, soldGb: 6 },
    );

    expect(screen.getByTestId(changeModelOptionId('qwen3-4b'))).toBeDisabled();
    expect(screen.getByTestId(changeModelOptionId('qwen3-4b'))).toHaveTextContent(
      'Needs 8 GB of memory. This phone has 6 GB.',
      { exact: false },
    );
  });

  it('from voice off, Download chooses and starts the download', async () => {
    const { models } = await render({
      phase: 'skipped',
      resolved: null,
      files: [],
      choice: 'qwen3-1.7b',
    });
    await fireEvent.press(screen.getByTestId(changeModelOptionId('qwen3-4b')));

    await fireEvent.press(screen.getByTestId(changeModelIds.download));

    expect(models.calls).toEqual([
      { method: 'choose', choice: 'qwen3-4b' },
      { method: 'download' },
    ]);
  });
});
