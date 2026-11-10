import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { FakeModelStore } from '../test-support/voice';
import { installFakeEngines } from '../test-support/voiceUi';
import { actionId } from '../ui/testIds';
import type { ModelStatus } from '../voice/contracts';
import { DownloadBanner } from '../voice/DownloadBanner';
import { downloadBannerIds } from '../voice/DownloadBanner.testIds';
import { MODEL_FILES } from '../voice/manifest';
import { memoryView } from './bannerView';
import { formatGb } from './modelChoice';
import { MemorySheet } from './MemorySheet';
import { memorySheetIds } from './MemorySheet.testIds';

// The manifest's sizes are pinned by another task: sized entries so the label has real figures to match.
jest.mock('../voice/manifest', () => {
  const actual = jest.requireActual<typeof import('../voice/manifest')>('../voice/manifest');
  const sizes = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
  return {
    ...actual,
    MODEL_FILES: actual.MODEL_FILES.map((f) => ({
      ...f,
      bytes: sizes[f.model] / actual.MODEL_FILES.filter((g) => g.model === f.model).length,
    })),
  };
});

const failedMemory: Partial<ModelStatus> = {
  phase: 'failed',
  reason: 'memory',
  resolved: 'qwen3-4b',
};
const smallName = MODEL_FILES.find((f) => f.model === 'qwen3-1.7b')?.name;
const sizeOfSmall = MODEL_FILES.filter((f) => f.model === 'qwen3-1.7b').reduce(
  (sum, f) => sum + f.bytes,
  0,
);
const bigName = MODEL_FILES.find((f) => f.model === 'qwen3-4b')?.name;

async function renderSheet() {
  const models = new FakeModelStore();
  models.set(failedMemory);
  installFakeEngines({ models });
  const onClose = jest.fn();
  await renderScreen(<MemorySheet open view={memoryView(models.status())} onClose={onClose} />);
  return { models, onClose };
}

describe('MemorySheet', () => {
  it('says the failed model needs more memory and names the smaller one with its size', async () => {
    await renderSheet();

    expect(screen.getByTestId(memorySheetIds.body)).toHaveTextContent(
      `${bigName} needs more memory than this phone has free. Voice is off.`,
    );
    expect(
      screen.getByLabelText(`Switch to ${smallName} (${formatGb(sizeOfSmall)})`),
    ).toBeOnTheScreen();
  });

  it('Switch to Qwen3 1.7B chooses 1.7B and downloads; Not now does neither', async () => {
    const first = await renderSheet();
    await fireEvent.press(screen.getByTestId(memorySheetIds.notNow));
    expect(first.models.calls).toEqual([]);
    expect(first.onClose).toHaveBeenCalledTimes(1);

    await fireEvent.press(screen.getByTestId(memorySheetIds.switchSmaller));
    expect(first.models.calls).toEqual([
      { method: 'choose', choice: 'qwen3-1.7b' },
      { method: 'download' },
    ]);
    expect(first.onClose).toHaveBeenCalledTimes(2);
  });

  it('offers no switch when the 1.7B model is the one that failed', async () => {
    const models = new FakeModelStore();
    models.set({ ...failedMemory, resolved: 'qwen3-1.7b' });
    installFakeEngines({ models });
    await renderScreen(<MemorySheet open view={memoryView(models.status())} onClose={jest.fn()} />);

    expect(screen.queryByTestId(memorySheetIds.switchSmaller)).toBeNull();
    expect(screen.getByTestId(memorySheetIds.notNow)).toBeOnTheScreen();
  });

  it('opens from the banner on a memory failure, and nothing downloads until Switch is pressed', async () => {
    const models = new FakeModelStore();
    models.set(failedMemory);
    installFakeEngines({ models });
    await renderScreen(<DownloadBanner placement="list" />);

    expect(models.calls).toEqual([]);
    await fireEvent.press(screen.getByTestId(actionId(downloadBannerIds.memory)));
    expect(screen.getByTestId(memorySheetIds.body)).toBeOnTheScreen();
    expect(models.calls).toEqual([]);
  });
});
