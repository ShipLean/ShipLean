import { act, fireEvent, screen, within } from '@testing-library/react-native';
import { links } from '../links';
import { fakeVoice, renderWithVoice, type FakeVoice } from '../test-support/voiceUi';
import { micLabelId, micSlotId, micStrikeId, type MicPlacement } from './MicSlot.testIds';
import { MicSlot } from './MicSlot';
import { micPermissionSheetId } from './sheets/MicPermissionSheet.testIds';
import type { VoiceUiState } from './internals';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush }),
}));

beforeEach(() => mockPush.mockClear());

const at = (locationX: number, locationY: number) => ({ nativeEvent: { locationX, locationY } });
const grant = (placement: MicPlacement = 'tabBar') =>
  fireEvent(screen.getByTestId(micSlotId(placement)), 'responderGrant', at(20, 20));
const release = (placement: MicPlacement = 'tabBar') =>
  fireEvent(screen.getByTestId(micSlotId(placement)), 'responderRelease', at(20, 20));

async function mount(state: VoiceUiState, placement: MicPlacement = 'tabBar'): Promise<FakeVoice> {
  const voice = fakeVoice(state);
  await renderWithVoice(<MicSlot placement={placement} />, { voice });
  return voice;
}

const idle: VoiceUiState = { kind: 'idle', permission: 'granted' };

// Icons are hidden from screen readers, so the queries look at hidden elements too.
const HIDDEN = { includeHiddenElements: true };

describe('MicSlot', () => {
  it('press in starts and release ends a hold', async () => {
    const voice = await mount(idle);
    expect(screen.getByTestId(micLabelId('tabBar'))).toHaveTextContent('Hold to talk');

    await grant();
    expect(voice.calls).toEqual(['holdStart']);
    await release();
    expect(voice.calls).toEqual(['holdStart', 'holdEnd']);
  });

  it('draws the tab bar mic as a 64 point icon button with its label outside it', async () => {
    await mount(idle);

    const button = screen.getByTestId(micSlotId('tabBar'));
    expect(button).toHaveStyle({ width: 64, height: 64, borderRadius: 32 });
    expect(within(button).getByTestId('icon-mic', HIDDEN)).toBeOnTheScreen();
    expect(screen.getByTestId(micLabelId('tabBar'))).toHaveTextContent('Hold to talk');
    expect(within(button).queryByTestId(micLabelId('tabBar'))).toBeNull();
  });

  it('keeps the dock mic 52 points with its label inside', async () => {
    await mount(idle, 'dock');

    const button = screen.getByTestId(micSlotId('dock'));
    expect(button).toHaveStyle({ width: 52, height: 52 });
    expect(within(button).getByTestId(micLabelId('dock'))).toHaveTextContent('Hold to talk');
  });

  it('dragging off while held cancels', async () => {
    const voice = await mount(idle);
    await grant();
    await fireEvent(screen.getByTestId(micSlotId('tabBar')), 'responderMove', at(20, -120));
    expect(voice.calls).toEqual(['holdStart', 'cancel']);
    // Letting go after the drag sends nothing.
    await release();
    expect(voice.calls).toEqual(['holdStart', 'cancel']);
  });

  it('moving a little while held keeps the hold', async () => {
    const voice = await mount(idle);
    await grant();
    await fireEvent(screen.getByTestId(micSlotId('tabBar')), 'responderMove', at(30, 10));
    await release();
    expect(voice.calls).toEqual(['holdStart', 'holdEnd']);
  });

  it('skipped shows Set up voice and a tap opens setup models', async () => {
    const voice = await mount({ kind: 'disabled', reason: 'skipped', permission: 'granted' });
    expect(screen.getByTestId(micLabelId('tabBar'))).toHaveTextContent('Set up voice');

    await grant();
    await release();
    expect(mockPush).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith(links.setup('models'));
    expect(voice.calls).toEqual([]);
  });

  it('noModels leads to setup models too', async () => {
    await mount({ kind: 'disabled', reason: 'noModels', permission: 'granted' });
    expect(screen.getByTestId(micLabelId('tabBar'))).toHaveTextContent('Set up voice');
    await grant();
    await release();
    expect(mockPush).toHaveBeenCalledWith(links.setup('models'));
  });

  it('downloading shows Downloading and a tap opens the downloading page', async () => {
    const voice = await mount({ kind: 'disabled', reason: 'downloading', permission: 'granted' });
    expect(screen.getByTestId(micLabelId('tabBar'))).toHaveTextContent('Downloading');

    await grant();
    await release();
    expect(mockPush).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith(links.setup('downloading'));
    expect(voice.calls).toEqual([]);
  });

  it('micOff shows the crossed-out Mic off; a tap and a hold both open the permission sheet', async () => {
    const voice = await mount({ kind: 'disabled', reason: 'micOff', permission: 'deniedFinal' });
    expect(screen.getByTestId(micLabelId('tabBar'))).toHaveTextContent('Mic off');
    expect(screen.getByTestId(micStrikeId('tabBar'))).toBeOnTheScreen();
    expect(screen.queryByTestId(micPermissionSheetId)).not.toBeOnTheScreen();

    // A tap: touch down and up at once.
    await grant();
    await release();
    expect(screen.getByTestId(micPermissionSheetId)).toBeOnTheScreen();
    expect(mockPush).not.toHaveBeenCalled();
    expect(voice.calls).toEqual([]);
  });

  it('micOff opens the permission sheet while the touch is still held', async () => {
    const voice = await mount({ kind: 'disabled', reason: 'micOff', permission: 'deniedCanAsk' });
    await grant();
    expect(screen.getByTestId(micPermissionSheetId)).toBeOnTheScreen();
    expect(voice.calls).toEqual([]);
  });

  it('undetermined permission opens the ask sheet on hold and never starts listening', async () => {
    const voice = await mount({ kind: 'idle', permission: 'undetermined' });
    await grant();
    expect(screen.getByTestId(micPermissionSheetId)).toBeOnTheScreen();
    await release();
    expect(voice.calls).toEqual([]);
  });

  it('shows no strike and no sheet when the mic is on', async () => {
    await mount(idle);
    expect(screen.queryByTestId(micStrikeId('tabBar'))).not.toBeOnTheScreen();
    expect(screen.queryByTestId(micPermissionSheetId)).not.toBeOnTheScreen();
  });

  it('renders the same looks in the dock variant', async () => {
    const voice = await mount(idle, 'dock');
    expect(screen.getByTestId(micLabelId('dock'))).toHaveTextContent('Hold to talk');
    await grant('dock');
    await release('dock');
    expect(voice.calls).toEqual(['holdStart', 'holdEnd']);

    await act(async () =>
      voice.set({ kind: 'disabled', reason: 'downloading', permission: 'granted' }),
    );
    expect(screen.getByTestId(micLabelId('dock'))).toHaveTextContent('Downloading');
    await grant('dock');
    await release('dock');
    expect(mockPush).toHaveBeenCalledWith(links.setup('downloading'));

    await act(async () =>
      voice.set({ kind: 'disabled', reason: 'micOff', permission: 'deniedFinal' }),
    );
    expect(screen.getByTestId(micStrikeId('dock'))).toBeOnTheScreen();
  });
});
