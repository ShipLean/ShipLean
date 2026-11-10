import { fireEvent, screen } from '@testing-library/react-native';
import { inboxApi } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { renderScreen } from '../test-support/renderScreen';
import { kvValues } from '../test-support/storage';
import { FromSetupTip } from './FromSetupTip';
import { fromSetupTipIds } from './FromSetupTip.testIds';
import { GUIDE_KEYS } from './guide';

let mockParams: Record<string, string> = {};
jest.mock('expo-router', () => ({
  ...jest.requireActual<typeof import('expo-router')>('expo-router'),
  useLocalSearchParams: () => mockParams,
}));

beforeEach(() => {
  kvValues.clear();
  mockParams = {};
});

describe('FromSetupTip', () => {
  it('shows nothing on an Inbox opened any other way', async () => {
    await renderScreen(<FromSetupTip projectKey="SHIP" />);

    expect(screen.getByTestId(fromSetupTipIds.root).children).toHaveLength(0);
    expect(kvValues.has(GUIDE_KEYS.fromSetupTipShown)).toBe(false);
  });

  it('names the items from the answer, and Got it puts it away', async () => {
    inboxApi({}, { inbox: designInbox() });
    mockParams = { from: 'setup' };
    await renderScreen(<FromSetupTip projectKey="SHIP" />);

    expect(await screen.findByText('The 6 items from your answer')).toBeOnTheScreen();
    expect(kvValues.get(GUIDE_KEYS.fromSetupTipShown)).toBe('1');
    await fireEvent.press(screen.getByTestId(fromSetupTipIds.gotIt));

    expect(screen.queryByTestId(fromSetupTipIds.card)).toBeNull();
  });
});
