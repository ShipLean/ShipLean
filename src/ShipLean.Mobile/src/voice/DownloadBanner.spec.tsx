import { act, fireEvent, screen } from '@testing-library/react-native';
import { links } from '../links';
import { MOBILE_DATA_ASKED_KEY } from '../setup/mobileDataAsk';
import { mobileDataSheetIds } from '../setup/MobileDataSheet.testIds';
import { SETUP_OFFERED_KEY } from '../setup/firstRun';
import { renderScreen } from '../test-support/renderScreen';
import { kvValues } from '../test-support/storage';
import { installFakeEngines } from '../test-support/voiceUi';
import { FakeModelStore } from '../test-support/voice';
import { actionId } from '../ui/testIds';
import type { ModelStatus } from './contracts';
import { DownloadBanner } from './DownloadBanner';
import { downloadBannerIds } from './DownloadBanner.testIds';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual<typeof import('expo-router')>('expo-router'),
  useRouter: () => ({ push: mockPush, back: jest.fn(), canGoBack: () => false }),
}));

beforeEach(() => {
  kvValues.clear();
});

async function render(set: Partial<ModelStatus>) {
  const models = new FakeModelStore();
  models.set(set);
  installFakeEngines({ models });
  await renderScreen(<DownloadBanner placement="list" />);
  return models;
}

const downloading: Partial<ModelStatus> = {
  phase: 'downloading',
  wifiOnly: true,
  bytesDone: 2_000_000_000,
  bytesTotal: 3_100_000_000,
};
const pausedForWifi: Partial<ModelStatus> = {
  phase: 'paused',
  reason: 'wifi',
  bytesDone: 2_000_000_000,
  bytesTotal: 3_100_000_000,
};

describe('DownloadBanner', () => {
  it('shows progress and a tap opens the Downloading page', async () => {
    await render(downloading);

    expect(screen.getByTestId(downloadBannerIds.progressText)).toHaveTextContent(
      'Voice is getting ready · 2.0 of 3.1 GB on Wi-Fi. Read, tap and type in the meantime.',
    );
    expect(screen.getByTestId(downloadBannerIds.progressBar)).toHaveAccessibilityValue({
      now: 65,
    });
    await fireEvent.press(screen.getByTestId(downloadBannerIds.progress));

    expect(mockPush).toHaveBeenCalledWith(links.setup('downloading'));
  });

  it('a pause for Wi-Fi on mobile data asks once; Use mobile data calls useMobileDataOnce; Wait does not', async () => {
    const models = await render(pausedForWifi);

    expect(screen.getByTestId(downloadBannerIds.progressText)).toHaveTextContent(
      'Voice download paused · waiting for Wi-Fi',
    );
    expect(screen.getByTestId(mobileDataSheetIds.body)).toHaveTextContent(
      "You're off Wi-Fi. 1.1 GB of the voice models is left to download.",
    );
    await fireEvent.press(screen.getByTestId(mobileDataSheetIds.wait));
    expect(models.calls).toEqual([]);
    expect(screen.queryByTestId(mobileDataSheetIds.root)).toBeNull();

    // Another status change while still paused for Wi-Fi does not ask again.
    await act(async () => models.set({ bytesDone: 2_000_000_001 }));
    expect(screen.queryByTestId(mobileDataSheetIds.root)).toBeNull();
    expect(kvValues.get(MOBILE_DATA_ASKED_KEY)).toBe('1');

    // The download ends; a later pause is a new download and asks again.
    await act(async () => models.set({ phase: 'ready', reason: null }));
    expect(kvValues.has(MOBILE_DATA_ASKED_KEY)).toBe(false);
    await act(async () => models.set(pausedForWifi));
    await fireEvent.press(screen.getByTestId(mobileDataSheetIds.useMobileData));
    expect(models.calls).toEqual([{ method: 'useMobileDataOnce' }]);
    expect(screen.queryByTestId(mobileDataSheetIds.root)).toBeNull();
  });

  it('does not ask on a pause that is not for Wi-Fi', async () => {
    await render({ ...pausedForWifi, reason: 'network' });

    expect(screen.getByTestId(downloadBannerIds.progressText)).toHaveTextContent(
      'Voice download paused · waiting for a connection',
    );
    expect(screen.queryByTestId(mobileDataSheetIds.root)).toBeNull();
  });

  it('skipped voice offers Set up, which opens the model screen', async () => {
    await render({ phase: 'skipped' });

    expect(screen.getByTestId(downloadBannerIds.off)).toHaveTextContent(/Voice is off/);
    await fireEvent.press(screen.getByTestId(actionId(downloadBannerIds.off)));

    expect(mockPush).toHaveBeenCalledWith(links.setup('models'));
  });

  it('shows Voice is off for none only once setup was offered', async () => {
    await render({ phase: 'none' });
    expect(screen.queryByTestId(downloadBannerIds.off)).toBeNull();
  });

  it('shows Voice is off for none after setup was offered', async () => {
    kvValues.set(SETUP_OFFERED_KEY, '1');
    await render({ phase: 'none' });
    expect(screen.getByTestId(downloadBannerIds.off)).toBeOnTheScreen();
  });

  it('keeps its root mounted and empty when voice is ready', async () => {
    await render({ phase: 'ready' });

    expect(screen.getByTestId(downloadBannerIds.root)).toBeOnTheScreen();
    expect(screen.getByTestId(downloadBannerIds.root).children).toHaveLength(0);
  });
});
