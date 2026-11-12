import { act, fireEvent, screen } from '@testing-library/react-native';
import { useState, type ReactElement } from 'react';
import { Pressable } from 'react-native';
import { links } from '../../links';
import { renderWithVoice, fakeVoice, installFakeEngines } from '../../test-support/voiceUi';
import type { ScreenVoiceSpec } from '../contracts';
import { primitiveIds } from '../../ui/testIds';
import { ToastHost } from '../../ui/Toast';
import { heardQuoteId, heardQuoteLabelId, heardQuoteTextId } from '../sheets/HeardQuote.testIds';
import { HeardFailure, type HeardFailureProps } from './HeardFailure';
import {
  heardFailureFixId,
  heardFailureNoteId,
  heardFailureNothingChangedId,
  heardFailureSheetId,
  heardFailureWhatCanISayId,
} from './HeardFailure.testIds';
import { resetSilentHolds } from './silentHolds';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush }),
}));

// A screen spec that says only what its context() holds.
const screenSpec = (context: Record<string, unknown>): ScreenVoiceSpec => ({
  screen: 'test',
  actions: [],
  context: () => context,
  hotwords: () => [],
  toActions: () => ({ ok: true, actions: [], lines: [], rejected: [], view: null }),
});
const focusedOn = (key: string) => screenSpec({ key });

type Heard = HeardFailureProps['failure'];

const silent = (): Heard => ({ kind: 'nothingHeard', detail: {} });
const short = (): Heard => ({ kind: 'tooShort', detail: { heldMs: 200 } });
const unclear = (heard: string): Heard => ({ kind: 'unclear', detail: { heard } });

function props(failure: Heard, over: Partial<HeardFailureProps> = {}): HeardFailureProps {
  return {
    failure,
    heard: null,
    source: 'voice',
    spec: null,
    onRetry: jest.fn(),
    onTypeInstead: jest.fn(),
    onWhatCanISay: jest.fn(),
    onClose: jest.fn(),
    ...over,
  };
}

// Re-renders HeardFailure with the same failure each time the bump is pressed.
function Bumpy(p: HeardFailureProps) {
  const [, setN] = useState(0);
  return (
    <ToastHost>
      <Pressable testID="bump" onPress={() => setN((n) => n + 1)} />
      <HeardFailure {...p} />
    </ToastHost>
  );
}

const withToasts = (p: HeardFailureProps): ReactElement => (
  <ToastHost>
    <HeardFailure {...p} />
  </ToastHost>
);

beforeEach(() => {
  resetSilentHolds();
  mockPush.mockClear();
  jest.useFakeTimers();
});
afterEach(() => jest.useRealTimers());

// One hold that failed: the sheet host mounts HeardFailure for the failure and unmounts it when the state rests.
async function hold(failure: Heard, over: Partial<HeardFailureProps> = {}) {
  const p = props(failure, over);
  const view = await renderWithVoice(withToasts(p));
  return { ...view, p };
}

describe('HeardFailure', () => {
  it('tooShort shows the hold hint toast and no sheet', async () => {
    installFakeEngines();
    const { p, unmount } = await hold(short());

    expect(screen.getByTestId(primitiveIds.toast)).toHaveTextContent(
      'Hold the mic while you speak',
    );
    expect(screen.queryByTestId(heardFailureSheetId)).toBeNull();
    expect(screen.queryByTestId(primitiveIds.sheetBackdrop)).toBeNull();
    expect(p.onClose).toHaveBeenCalledTimes(1);

    await act(async () => {
      jest.advanceTimersByTime(2999);
    });
    expect(screen.getByTestId(primitiveIds.toast)).toBeOnTheScreen();
    await act(async () => {
      jest.advanceTimersByTime(1);
    });
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
    await unmount();
  });

  it('nothingHeard shows the toast with nothing was changed and the input name', async () => {
    const engines = installFakeEngines();
    engines.recognizer.setInputName('AirPods Pro');
    const { p } = await hold(silent());

    expect(screen.getByTestId(primitiveIds.toast)).toHaveTextContent(
      "Didn't hear anything · nothing was changed · AirPods Pro",
    );
    expect(screen.queryByTestId(heardFailureSheetId)).toBeNull();
    expect(screen.queryByTestId(primitiveIds.sheetBackdrop)).toBeNull();
    expect(p.onClose).toHaveBeenCalledTimes(1);
  });

  it('nothingHeard leaves the input name out when the phone does not say', async () => {
    installFakeEngines();
    await hold(silent());
    expect(screen.getByTestId(primitiveIds.toast)).toHaveTextContent(
      "Didn't hear anything · nothing was changed",
      { exact: true },
    );
  });

  it('the first silent hold has no action and the second offers Tune mic, which opens the page', async () => {
    installFakeEngines();
    const first = await hold(silent());
    expect(screen.getByTestId(primitiveIds.toast)).toBeOnTheScreen();
    expect(screen.queryByTestId(primitiveIds.toastAction)).toBeNull();
    await first.unmount();

    const second = await hold(silent());
    expect(mockPush).not.toHaveBeenCalled();
    await fireEvent.press(screen.getByTestId(primitiveIds.toastAction));
    expect(mockPush).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith(links.micTuning('second_hold'));
    expect(mockPush.mock.calls[0][0]).toEqual({
      pathname: '/mic-tuning',
      params: { from: 'second_hold' },
    });
    await second.unmount();

    // The page opening reset the count: the next silent hold is a plain toast again.
    const next = await hold(silent());
    expect(screen.queryByTestId(primitiveIds.toastAction)).toBeNull();
    await next.unmount();
  });

  it('the third silent hold pushes the page once, even across re-renders, and shows no toast', async () => {
    installFakeEngines();
    for (let i = 0; i < 2; i++) await (await hold(silent())).unmount();
    mockPush.mockClear();
    const p = props(silent(), { spec: focusedOn('SHIP-12') });
    const view = await renderWithVoice(<Bumpy {...p} />);
    expect(mockPush).toHaveBeenCalledTimes(1);
    expect(mockPush.mock.calls[0][0]).toEqual({
      pathname: '/mic-tuning',
      params: { from: 'third_hold', key: 'SHIP-12' },
    });
    expect(p.onClose).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
    await fireEvent.press(screen.getByTestId('bump'));
    await fireEvent.press(screen.getByTestId('bump'));
    expect(mockPush).toHaveBeenCalledTimes(1);
    await view.unmount();

    // The count was reset by the push, so the next silent hold is a plain toast.
    const after = await hold(silent());
    expect(mockPush).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId(primitiveIds.toastAction)).toBeNull();
    expect(screen.getByTestId(primitiveIds.toast)).toBeOnTheScreen();
    await after.unmount();
  });

  it('falls back to the first visible key when the screen is not on an item', async () => {
    installFakeEngines();
    for (let i = 0; i < 2; i++) await (await hold(silent())).unmount();
    mockPush.mockClear();
    await renderWithVoice(
      withToasts(
        props(silent(), { spec: screenSpec({ rows: [{ key: 'SHIP-7', title: 'Seven' }] }) }),
      ),
    );
    expect(mockPush.mock.calls[0][0]).toEqual({
      pathname: '/mic-tuning',
      params: { from: 'third_hold', key: 'SHIP-7' },
    });
  });

  it('a success in between, or a different failure, resets the count', async () => {
    installFakeEngines();
    for (let i = 0; i < 2; i++) await (await hold(silent())).unmount();
    resetSilentHolds();
    await (await hold(silent())).unmount();
    expect(mockPush).not.toHaveBeenCalled();

    resetSilentHolds();
    for (let i = 0; i < 2; i++) await (await hold(silent())).unmount();
    await (await hold(short())).unmount();
    const afterShort = await hold(silent());
    expect(mockPush).not.toHaveBeenCalled();
    expect(screen.queryByTestId(primitiveIds.toastAction)).toBeNull();
    await afterShort.unmount();
  });

  it('unclear offers Add as a note for voice and Post as a note for typed, which also applies', async () => {
    installFakeEngines();
    const voiceHeard = 'Hmm, the S22 thing, what was it';

    // Voice: Add as a note retries as a note and goes back to a preview; nothing is applied from here.
    const voice = fakeVoice();
    const v = props(unclear(voiceHeard), { heard: voiceHeard, source: 'voice' });
    const first = await renderWithVoice(<HeardFailure {...v} />, { voice });
    expect(screen.getByTestId(heardFailureSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(heardFailureNoteId)).toHaveTextContent('Add as a note');
    expect(screen.getByTestId(heardFailureFixId)).toHaveTextContent('Fix');
    await fireEvent.press(screen.getByTestId(heardFailureNoteId));
    expect(v.onRetry).toHaveBeenCalledWith({ asNote: true });
    expect(voice.calls).toEqual([]);
    await fireEvent.press(screen.getByTestId(heardFailureFixId));
    expect(voice.calls).toEqual(['fix']);
    await first.unmount();

    // Typed: Post as a note retries as a note and then applies at once.
    const typedVoice = fakeVoice();
    const t = props(unclear('the S22 runs warm'), { heard: 'the S22 runs warm', source: 'typed' });
    await renderWithVoice(<HeardFailure {...t} />, { voice: typedVoice });
    expect(screen.getByTestId(heardFailureNoteId)).toHaveTextContent('Post as a note');
    expect(screen.getByTestId(heardFailureFixId)).toHaveTextContent('Edit');
    await fireEvent.press(screen.getByTestId(heardFailureNoteId));
    expect(t.onRetry).toHaveBeenCalledWith({ asNote: true });
    expect(typedVoice.calls).toEqual(['apply']);
    await fireEvent.press(screen.getByTestId(heardFailureFixId));
    expect(typedVoice.calls).toEqual(['apply', 'fix']);
  });

  it('unclear passes the heard words to HeardQuote and says nothing was changed', async () => {
    installFakeEngines();
    const words = 'Hmm, the S22 thing, what was it, the warm one';
    await renderWithVoice(
      <HeardFailure {...props(unclear(words), { heard: words, source: 'voice' })} />,
    );
    expect(screen.getByTestId(heardQuoteTextId(heardQuoteId))).toHaveTextContent(`“${words}”`);
    expect(screen.getByTestId(heardQuoteLabelId(heardQuoteId))).toHaveTextContent('Heard');
    expect(screen.getByTestId(heardFailureNothingChangedId)).toHaveTextContent(
      'Nothing was changed',
    );
  });

  it('unclear keeps the words from the failure when the state has none, and says You typed for typed', async () => {
    installFakeEngines();
    await renderWithVoice(
      <HeardFailure {...props(unclear('move it'), { heard: null, source: 'typed' })} />,
    );
    expect(screen.getByTestId(heardQuoteTextId(heardQuoteId))).toHaveTextContent('move it');
    expect(screen.getByTestId(heardQuoteLabelId(heardQuoteId))).toHaveTextContent('You typed');
  });

  it('What can I say here? calls onWhatCanISay', async () => {
    installFakeEngines();
    const p = props(unclear('hmm'), { heard: 'hmm' });
    await renderWithVoice(<HeardFailure {...p} />);
    await fireEvent.press(screen.getByTestId(heardFailureWhatCanISayId));
    expect(p.onWhatCanISay).toHaveBeenCalledTimes(1);
  });
});
