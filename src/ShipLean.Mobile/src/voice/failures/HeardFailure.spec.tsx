import { act, fireEvent, screen } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { renderWithVoice, fakeVoice, installFakeEngines } from '../../test-support/voiceUi';
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
import { micCheckId, micCheckInputId } from './MicCheck.testIds';
import { resetSilentHolds } from './silentHolds';

type Heard = HeardFailureProps['failure'];

const silent = (): Heard => ({ kind: 'nothingHeard', detail: {} });
const short = (): Heard => ({ kind: 'tooShort', detail: { heldMs: 200 } });
const unclear = (heard: string): Heard => ({ kind: 'unclear', detail: { heard } });

function props(failure: Heard, over: Partial<HeardFailureProps> = {}): HeardFailureProps {
  return {
    failure,
    heard: null,
    source: 'voice',
    onRetry: jest.fn(),
    onTypeInstead: jest.fn(),
    onWhatCanISay: jest.fn(),
    onClose: jest.fn(),
    ...over,
  };
}

const withToasts = (p: HeardFailureProps): ReactElement => (
  <ToastHost>
    <HeardFailure {...p} />
  </ToastHost>
);

beforeEach(() => {
  resetSilentHolds();
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
    expect(screen.queryByTestId(micCheckId)).toBeNull();
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
    expect(screen.queryByTestId(micCheckId)).toBeNull();
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

  it('the third nothingHeard in a row opens the mic check, and a success in between resets the count', async () => {
    const engines = installFakeEngines();
    engines.recognizer.setInputName('AirPods Pro');

    for (let i = 0; i < 2; i++) {
      const { unmount } = await hold(silent());
      expect(screen.queryByTestId(micCheckId)).toBeNull();
      await unmount();
    }
    const third = await hold(silent());
    expect(screen.getByTestId(micCheckId)).toBeOnTheScreen();
    expect(screen.getByTestId(micCheckInputId)).toHaveTextContent(/AirPods Pro/);
    expect(third.p.onClose).not.toHaveBeenCalled();
    await third.unmount();

    // Two silent holds, then a hold that was understood: the next silent hold counts as the first again.
    resetSilentHolds();
    for (let i = 0; i < 2; i++) await (await hold(silent())).unmount();
    resetSilentHolds();
    const afterSuccess = await hold(silent());
    expect(screen.queryByTestId(micCheckId)).toBeNull();
    expect(screen.getByTestId(primitiveIds.toast)).toBeOnTheScreen();
    await afterSuccess.unmount();

    // A different failure in between resets it too.
    for (let i = 0; i < 2; i++) await (await hold(silent())).unmount();
    await (await hold(short())).unmount();
    const afterShort = await hold(silent());
    expect(screen.queryByTestId(micCheckId)).toBeNull();
    await afterShort.unmount();
  });

  it('opens Type instead from the mic check', async () => {
    installFakeEngines();
    for (let i = 0; i < 2; i++) await (await hold(silent())).unmount();
    const { p } = await hold(silent());
    await fireEvent.press(screen.getByTestId('mic-check-type-instead'));
    expect(p.onTypeInstead).toHaveBeenCalledTimes(1);
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
