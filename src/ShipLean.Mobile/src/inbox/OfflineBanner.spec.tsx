import { act, screen } from '@testing-library/react-native';
import { setConnected } from '../test-support/netInfo';
import { renderScreen } from '../test-support/renderScreen';
import type { Scheme } from '../theme/themes';
import { OfflineBanner } from './OfflineBanner';
import { inboxIds } from './testIds';

const TEXT = "You're offline. Your Inbox as of 09:12. Answers send when you're back.";

afterEach(async () => {
  await act(async () => setConnected(true));
});

describe.each<Scheme>(['light', 'dark'])('OfflineBanner (%s)', (scheme) => {
  it('shows the banner with the loaded-at label when offline', async () => {
    await renderScreen(<OfflineBanner loadedAtLabel="09:12" />, { scheme });
    expect(screen.queryByTestId(inboxIds.offlineBanner)).toBeNull();

    await act(async () => setConnected(false));

    expect(screen.getByTestId(inboxIds.offlineBanner)).toBeOnTheScreen();
    expect(screen.getByText(TEXT)).toBeOnTheScreen();
  });

  it('shows the banner at once when the screen opens offline', async () => {
    await act(async () => setConnected(false));
    await renderScreen(<OfflineBanner loadedAtLabel="09:12" />, { scheme });
    expect(screen.getByText(TEXT)).toBeOnTheScreen();
  });

  it('uses the label as given, never reformatting the time', async () => {
    await act(async () => setConnected(false));
    await renderScreen(<OfflineBanner loadedAtLabel="3:07 pm" />, { scheme });
    expect(screen.getByText(/Your Inbox as of 3:07 pm\./)).toBeOnTheScreen();
  });

  it('hides the banner when back online', async () => {
    await act(async () => setConnected(false));
    await renderScreen(<OfflineBanner loadedAtLabel="09:12" />, { scheme });
    expect(screen.getByTestId(inboxIds.offlineBanner)).toBeOnTheScreen();

    await act(async () => setConnected(true));

    expect(screen.queryByTestId(inboxIds.offlineBanner)).toBeNull();
    expect(screen.queryByText(TEXT)).toBeNull();
  });
});
