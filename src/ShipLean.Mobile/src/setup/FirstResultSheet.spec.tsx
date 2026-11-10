import { act, fireEvent, screen } from '@testing-library/react-native';
import { links } from '../links';
import { inboxApi } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { renderScreen } from '../test-support/renderScreen';
import { kvValues } from '../test-support/storage';
import { FakeVoiceProvider, fakeVoice, installFakeEngines } from '../test-support/voiceUi';
import type { VoiceUiState } from '../voice/internals';
import { FirstResultSheet } from './FirstResultSheet';
import { firstResultRowId, firstResultSheetIds } from './FirstResultSheet.testIds';
import { FromSetupTip } from './FromSetupTip';
import { fromSetupTipIds } from './FromSetupTip.testIds';
import { GUIDE_KEYS } from './guide';
import { useFirstCommand } from './useFirstCommand';

const mockPush = jest.fn();
let mockParams: Record<string, string> = {};
jest.mock('expo-router', () => ({
  ...jest.requireActual<typeof import('expo-router')>('expo-router'),
  useRouter: () => ({ push: mockPush, back: jest.fn(), canGoBack: () => false }),
  useLocalSearchParams: () => mockParams,
}));

beforeEach(() => {
  kvValues.clear();
  mockParams = {};
});

const inbox = designInbox();

function Host() {
  const guide = useFirstCommand('SHIP');
  return (
    <FirstResultSheet
      open={guide.open}
      projectId={inbox.projectId}
      projectKey="SHIP"
      onOpenInbox={guide.onOpenInbox}
      onClose={guide.onClose}
    />
  );
}

const answer: VoiceUiState = { kind: 'answer', content: { lead: 'x', rows: [] } };

describe('FirstResultSheet', () => {
  it('lists the Inbox items with their counts and Open Inbox opens the Inbox once with the tip', async () => {
    installFakeEngines();
    const api = inboxApi({}, { inbox });
    // The coach mark was shown; the first answer comes next.
    kvValues.set(GUIDE_KEYS.voiceReadyShown, '1');
    const voice = fakeVoice({ kind: 'idle', permission: 'granted' });
    await renderScreen(
      <FakeVoiceProvider voice={voice}>
        <Host />
      </FakeVoiceProvider>,
    );
    expect(screen.queryByTestId(firstResultSheetIds.root)).toBeNull();

    await act(async () => voice.set(answer));

    // The answer sheet is closed, and the sheet lists the Inbox's items in its order.
    expect(voice.calls).toContain('cancel');
    expect(await screen.findByTestId(firstResultSheetIds.root)).toBeOnTheScreen();
    expect(await screen.findByTestId(firstResultSheetIds.lead)).toHaveTextContent(
      '6 items wait on you: 10 decisions, 3 questions, a mention and a status line to check.',
    );
    expect(api.callsTo(`GET /api/projects/${inbox.projectId}/inbox`)).toHaveLength(1);
    // In the Inbox's order, each with its own counts.
    const rowTexts = [
      ['SHIP-743', 'SHIP-743Nightly backupsstatus line'],
      ['SHIP-892', 'SHIP-892Demo artifacts1 question'],
      ['SHIP-907', 'SHIP-907Mobile app foundations8 decisions'],
      ['SHIP-924', 'SHIP-924Development build on both test phones1 question'],
      ['SHIP-742', 'SHIP-742GitHub issues sync1 mention'],
      ['SHIP-906', 'SHIP-906Voice commands2 decisions \u00b7 1 question'],
    ];
    expect(inbox.items.map((i) => i.key)).toEqual(rowTexts.map(([key]) => key));
    for (const [key, text] of rowTexts) {
      expect(screen.getByTestId(firstResultRowId(key))).toHaveTextContent(text);
    }
    expect(screen.getByTestId(firstResultSheetIds.unchanged)).toHaveTextContent(
      'From your Inbox as it is now \u00b7 nothing was changed',
    );

    await fireEvent.press(screen.getByTestId(firstResultSheetIds.openInbox));

    expect(mockPush).toHaveBeenCalledWith(links.inbox('SHIP', 'setup'));
    expect(kvValues.has(GUIDE_KEYS.firstCommandShown)).toBe(true);
    expect(screen.queryByTestId(firstResultSheetIds.root)).toBeNull();

    // The Inbox shows the tip once.
    mockParams = { from: 'setup' };
    const tip = await renderScreen(<FromSetupTip projectKey="SHIP" />);
    expect(await screen.findByTestId(fromSetupTipIds.card)).toBeOnTheScreen();
    await tip.unmount();
    await renderScreen(<FromSetupTip projectKey="SHIP" />);
    expect(screen.queryByTestId(fromSetupTipIds.card)).toBeNull();
  });

  it('Done closes it without opening the Inbox, and a later answer is just an answer', async () => {
    installFakeEngines();
    inboxApi({}, { inbox });
    kvValues.set(GUIDE_KEYS.voiceReadyShown, '1');
    const voice = fakeVoice({ kind: 'idle', permission: 'granted' });
    await renderScreen(
      <FakeVoiceProvider voice={voice}>
        <Host />
      </FakeVoiceProvider>,
    );
    await act(async () => voice.set(answer));
    await fireEvent.press(await screen.findByTestId(firstResultSheetIds.done));

    expect(mockPush).not.toHaveBeenCalled();
    expect(screen.queryByTestId(firstResultSheetIds.root)).toBeNull();
    expect(kvValues.has(GUIDE_KEYS.firstCommandShown)).toBe(true);

    voice.calls.length = 0;
    await act(async () => voice.set({ kind: 'idle', permission: 'granted' }));
    await act(async () => voice.set(answer));
    expect(voice.calls).not.toContain('cancel');
    expect(screen.queryByTestId(firstResultSheetIds.root)).toBeNull();
  });

  it('a result that changes things leaves the guide pending and the sheet shut', async () => {
    installFakeEngines();
    kvValues.set(GUIDE_KEYS.voiceReadyShown, '1');
    const voice = fakeVoice({ kind: 'idle', permission: 'granted' });
    await renderScreen(
      <FakeVoiceProvider voice={voice}>
        <Host />
      </FakeVoiceProvider>,
    );

    await act(async () =>
      voice.set({
        kind: 'preview',
        heard: 'add a note',
        source: 'voice',
        lines: [],
        removed: [],
        marks: [],
        fixing: false,
      }),
    );

    expect(screen.queryByTestId(firstResultSheetIds.root)).toBeNull();
    expect(voice.calls).not.toContain('cancel');
    expect(kvValues.has(GUIDE_KEYS.firstCommandShown)).toBe(false);
  });
});
