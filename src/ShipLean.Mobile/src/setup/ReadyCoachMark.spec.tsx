import { act, fireEvent, screen } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { inboxApi } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { renderScreen } from '../test-support/renderScreen';
import { kvValues } from '../test-support/storage';
import { FakeModelStore } from '../test-support/voice';
import { FakeVoiceProvider, fakeVoice, installFakeEngines } from '../test-support/voiceUi';
import { FirstResultSheet } from './FirstResultSheet';
import { firstResultSheetIds } from './FirstResultSheet.testIds';
import type { VoiceUiState } from '../voice/internals';
import { layout } from '../ui/layout';
import { GUIDE_KEYS } from './guide';
import { ReadyCoachMark } from './ReadyCoachMark';
import { readyCoachMarkIds } from './ReadyCoachMark.testIds';
import { useFirstCommand } from './useFirstCommand';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual<typeof import('expo-router')>('expo-router'),
  useRouter: () => ({ push: mockPush, back: jest.fn(), canGoBack: () => false }),
}));

beforeEach(() => {
  kvValues.clear();
});

function setup(
  status: Parameters<FakeModelStore['set']>[0],
  mic: 'granted' | 'deniedFinal' | 'undetermined' = 'granted',
) {
  const models = new FakeModelStore();
  models.set(status);
  const fakes = installFakeEngines({ models });
  fakes.mic.set(mic);
  return fakes;
}

// The coach mark with the guided command behind it, as the tab bar will mount them.
function Guide({ onSkip = jest.fn() }: { onSkip?: () => void }): ReactElement {
  const guide = useFirstCommand('SHIP');
  return (
    <>
      <ReadyCoachMark onTry={jest.fn()} onSkip={onSkip} />
      <FirstResultSheet
        open={guide.open}
        projectId={designInbox().projectId}
        projectKey="SHIP"
        onOpenInbox={guide.onOpenInbox}
        onClose={guide.onClose}
      />
    </>
  );
}

const answer: VoiceUiState = {
  kind: 'answer',
  content: { lead: '6 items wait on you', rows: [] },
};

describe('ReadyCoachMark', () => {
  it('shows once when voice first becomes ready, never again after Skip', async () => {
    const { models } = setup({ phase: 'downloading' });
    const onSkip = jest.fn();
    const first = await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={onSkip} />);
    expect(screen.getByTestId(readyCoachMarkIds.root)).toBeOnTheScreen();
    expect(screen.queryByTestId(readyCoachMarkIds.card)).toBeNull();

    await act(async () => models.set({ phase: 'ready' }));
    expect(await screen.findByTestId(readyCoachMarkIds.body)).toHaveTextContent(
      'Try it: hold the button and say ‘What needs me?’',
    );

    await fireEvent.press(screen.getByTestId(readyCoachMarkIds.skip));
    expect(screen.queryByTestId(readyCoachMarkIds.card)).toBeNull();
    expect(onSkip).toHaveBeenCalledTimes(1);

    // The next launch: ready again, and it stays away.
    await first.unmount();
    await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={onSkip} />);
    await act(async () => models.set({ phase: 'ready' }));
    expect(screen.queryByTestId(readyCoachMarkIds.card)).toBeNull();
    expect(screen.getByTestId(readyCoachMarkIds.root)).toBeOnTheScreen();
  });

  it('is not shown again on a later launch even without pressing Skip', async () => {
    setup({ phase: 'ready' });
    const first = await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={jest.fn()} />);
    expect(await screen.findByTestId(readyCoachMarkIds.card)).toBeOnTheScreen();
    await first.unmount();

    await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={jest.fn()} />);
    expect(screen.queryByTestId(readyCoachMarkIds.card)).toBeNull();
  });

  it('with the microphone off shows the turn-it-on variant and holds the guided command until it is granted', async () => {
    const fakes = setup({ phase: 'ready' }, 'undetermined');
    inboxApi({}, { inbox: designInbox() });
    const voice = fakeVoice({ kind: 'idle', permission: 'undetermined' });
    await renderScreen(
      <FakeVoiceProvider voice={voice}>
        <Guide />
      </FakeVoiceProvider>,
    );

    expect(await screen.findByTestId(readyCoachMarkIds.micOffCard)).toBeOnTheScreen();
    expect(screen.getByTestId(readyCoachMarkIds.body)).toHaveTextContent(
      'Turn on the microphone to use it',
    );
    expect(screen.queryByTestId(readyCoachMarkIds.card)).toBeNull();

    // An answer now is just an answer: the guide waits for the microphone.
    await act(async () => voice.set(answer));
    expect(screen.queryByTestId(firstResultSheetIds.root)).toBeNull();
    expect(voice.calls).not.toContain('cancel');
    expect(kvValues.has(GUIDE_KEYS.firstCommandShown)).toBe(false);
    await act(async () => voice.set({ kind: 'idle', permission: 'undetermined' }));

    await fireEvent.press(screen.getByTestId(readyCoachMarkIds.allowMic));
    expect(fakes.mic.requests).toBe(1);
    expect(await screen.findByTestId(readyCoachMarkIds.card)).toBeOnTheScreen();

    // The microphone is on and the coach mark was shown: the next answer opens the first-result sheet.
    await act(async () => voice.set(answer));
    expect(voice.calls).toContain('cancel');
    expect(await screen.findByTestId(firstResultSheetIds.openInbox)).toBeOnTheScreen();
  });

  it('offers Open Settings when the phone can no longer ask, and Not now puts it away for good', async () => {
    const fakes = setup({ phase: 'ready' }, 'deniedFinal');
    const first = await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={jest.fn()} />);

    await fireEvent.press(await screen.findByTestId(readyCoachMarkIds.openSettings));
    expect(fakes.mic.settingsOpened).toBe(1);
    expect(screen.queryByTestId(readyCoachMarkIds.allowMic)).toBeNull();
    await fireEvent.press(screen.getByTestId(readyCoachMarkIds.notNow));
    expect(screen.queryByTestId(readyCoachMarkIds.micOffCard)).toBeNull();

    await first.unmount();
    await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={jest.fn()} />);
    expect(screen.queryByTestId(readyCoachMarkIds.micOffCard)).toBeNull();
  });

  it('keeps its root mounted and empty while voice is not ready', async () => {
    setup({ phase: 'downloading' });
    await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={jest.fn()} />);

    expect(screen.getByTestId(readyCoachMarkIds.root).children).toHaveLength(0);
  });

  it('sits above the raised mic and its ring, so the card never covers the button', async () => {
    const { models } = setup({ phase: 'downloading' });
    await renderScreen(<ReadyCoachMark onTry={jest.fn()} onSkip={jest.fn()} />);
    await act(async () => models.set({ phase: 'ready' }));
    const card = await screen.findByTestId(readyCoachMarkIds.card);
    const { bottom } = card.props.style as { bottom: number };
    expect(bottom).toBeGreaterThan(layout.tabBar + layout.micRaise + layout.micRing);
  });
});
