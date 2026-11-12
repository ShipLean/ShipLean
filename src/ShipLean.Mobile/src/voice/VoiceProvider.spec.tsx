import { act, fireEvent, renderHook, screen } from '@testing-library/react-native';
import { useEffect } from 'react';
import { AppState, Text, type AppStateStatus } from 'react-native';
import { router } from 'expo-router';
import type { AppAction, PreviewLine } from '../actions/types';
import { aPending, fakeRunner } from '../test-support/actions';
import {
  aTranscript,
  FakeCommandModel,
  FakeMicPermission,
  FakeRecognizer,
} from '../test-support/voice';
import { renderWithVoice } from '../test-support/voiceUi';
import { useComposerRequest } from './typed/composerRequest';
import { ToastHost } from '../ui/Toast';
import { primitiveIds } from '../ui/testIds';
import type { ParsedCall, RecognitionSession, ScreenVoiceSpec, ViewIntent } from './contracts';
import { requestTypeInstead } from './micTuning/returnIntent';
import { onApplyFailed } from './flows/applyFailed';
import { onConflict } from './flows/conflict';
import { tooShortToast } from './failures/heardCopy';
import { fixHeard, startFix } from './flows/fix';
import { submitTyped } from './flows/typed';
import { useVoiceInternals, type VoiceInternals, type VoiceUiState } from './internals';
import {
  answerDoneId,
  answerLeadId,
  answerRowId,
  answerSheetId,
} from './sheets/AnswerSheet.testIds';
import { listeningPartialId } from './sheets/ListeningSheet.testIds';
import { previewLineId, previewSheetId } from './sheets/PreviewSheet.testIds';
import {
  useScreenVoice,
  useVoice,
  VoiceSheetsHost,
  type ScreenViewHandler,
  type VoiceApi,
} from './VoiceProvider';

// The provider's screen is not inside a navigator here, so a fake navigation object is the context's default: a test
// calls focusScreen() as a focus event would.
const mockFocusListeners = new Set<() => void>();
const focusScreen = () => {
  for (const cb of [...mockFocusListeners]) cb();
};
jest.mock('expo-router', () => ({
  ...jest.requireActual<object>('expo-router'),
  router: { push: jest.fn() },
}));
jest.mock('expo-router/react-navigation', () => {
  const { createContext } = jest.requireActual<typeof import('react')>('react');
  const nav = {
    isFocused: () => true,
    addListener: (event: string, cb: () => void) => {
      if (event === 'focus') mockFocusListeners.add(cb);
      return () => mockFocusListeners.delete(cb);
    },
  };
  return {
    ...jest.requireActual<object>('expo-router/react-navigation'),
    NavigationContext: createContext(nav),
  };
});

jest.mock('./flows/fix', () => ({ startFix: jest.fn(), fixHeard: jest.fn(async () => {}) }));
jest.mock('./flows/typed', () => ({ submitTyped: jest.fn(async () => {}) }));
jest.mock('./flows/conflict', () => ({ onConflict: jest.fn() }));
jest.mock('./flows/applyFailed', () => ({
  onApplyFailed: jest.fn(),
  retryApply: jest.fn(),
  queueApply: jest.fn(),
  discardApply: jest.fn(),
}));

const NOW = new Date('2030-01-01T09:00:00.000Z');

const action = (kind: string): AppAction => ({
  area: 'workItem',
  kind,
  itemId: 'item-906',
  key: 'SHIP-906',
});
const TEXT: Record<string, string> = {
  setState: 'State: New → Active',
  addNote: 'Note on SHIP-906',
};
const VIEWS: Record<string, ViewIntent> = {
  howMany: {
    kind: 'answer',
    content: {
      lead: 'Two Features need you',
      rows: [{ key: 'SHIP-102', title: 'Search', counts: '2 asks' }],
    },
  },
  open: { kind: 'navigate', link: '/p/SHIP/items/SHIP-102' as never },
  show: { kind: 'screen', command: 'show', args: { view: 'toDecide' } },
};

// Each call is one change (its line worded from TEXT), or, when it names a view, that view with no change.
const workItemSpec: ScreenVoiceSpec = {
  screen: 'SHIP-906 Voice commands',
  actions: [...Object.keys(TEXT), ...Object.keys(VIEWS)].map((name) => ({
    name,
    description: name,
    args: { type: 'object' },
  })),
  context: () => ({}),
  hotwords: () => ['SHIP-906', 'Active'],
  toActions: (calls: ParsedCall[]) => {
    const view = calls.map((c) => VIEWS[c.action]).find((v) => v !== undefined) ?? null;
    const changes = calls.filter((c) => VIEWS[c.action] === undefined);
    return {
      ok: true,
      actions: changes.map((c) => action(c.action)),
      lines: changes.map((c): PreviewLine => ({
        id: c.action,
        icon: 'flag',
        text: TEXT[c.action],
        removable: true,
      })),
      rejected: [],
      view,
    };
  },
};

// states: every state the screen rendered, so a state a sheet closes at once (a toast) is still seen.
const probe: {
  voice: VoiceApi | null;
  internals: VoiceInternals | null;
  states: VoiceUiState[];
} = {
  voice: null,
  internals: null,
  states: [],
};
// freshSpec builds the spec on every render, as the screens do.
function Screen({ onView, freshSpec }: { onView?: ScreenViewHandler; freshSpec?: boolean }) {
  useScreenVoice(freshSpec === true ? { ...workItemSpec } : workItemSpec, { onView });
  const voice = useVoice();
  const internals = useVoiceInternals();
  useEffect(() => {
    probe.voice = voice;
    probe.internals = internals;
    probe.states.push(voice.state);
  });
  return (
    <>
      <Text testID="voice-kind">{voice.state.kind}</Text>
      <VoiceSheetsHost />
    </>
  );
}
const voice = () => probe.voice!;
const internals = () => probe.internals!;
const kind = () => screen.getByTestId('voice-kind').props.children as string;
const SET_STATE: ParsedCall[] = [{ action: 'setState', args: {} }];
// A session whose stop() rejects, as a native recognizer may.
const brokenSession = (): RecognitionSession => ({
  onPartial: () => {},
  onLevel: () => {},
  frames: () => [],
  stop: () => Promise.reject(new Error('The recognizer stopped.')),
  cancel: () => {},
});

async function mount(onView?: ScreenViewHandler, freshSpec = false) {
  const recognizer = new FakeRecognizer();
  const model = new FakeCommandModel();
  const mic = new FakeMicPermission();
  const runner = fakeRunner();
  probe.states = [];
  // Words to hear unless a test scripts others.
  recognizer.script([], 'do it');
  // In the app the session's ToastHost is above every screen: a tap or a silent hold is a toast.
  await renderWithVoice(
    <ToastHost>
      <Screen onView={onView} freshSpec={freshSpec} />
    </ToastHost>,
    {
      engines: { recognizer, model, mic },
      runner,
    },
  );
  return { recognizer, model, mic, runner };
}

// Press, hold for ms, release, and let the parse settle.
async function hold(ms = 900) {
  await act(async () => voice().holdStart());
  await act(async () => {
    jest.advanceTimersByTime(ms);
  });
  await act(async () => voice().holdEnd());
}

beforeEach(() => {
  jest.useFakeTimers({ now: NOW });
  jest.clearAllMocks();
});
afterEach(() => jest.useRealTimers());

describe('VoiceProvider', () => {
  it('opens the composer in Command mode, once, when the page asked for Type instead and the screen regains focus', async () => {
    await mount();
    const composer = await renderHook(() => useComposerRequest());
    expect(composer.result.current.request).toBeNull();

    // No request from the Mic tuning page: a focus opens nothing.
    await act(async () => focusScreen());
    expect(composer.result.current.request).toBeNull();

    requestTypeInstead();
    await act(async () => focusScreen());
    expect(composer.result.current.request).toMatchObject({ mode: 'command', text: '' });

    // The dock took it; a later focus does not open it again.
    await act(async () => composer.result.current.consume());
    await act(async () => focusScreen());
    expect(composer.result.current.request).toBeNull();
  });

  it('hold, release and parse show the preview lines from resolveCommand', async () => {
    const { recognizer, model } = await mount();
    recognizer.script(['set this'], 'set this to Active and add a note: fine for v1');
    model.respond([
      { action: 'setState', args: {} },
      { action: 'addNote', args: {} },
    ]);

    await act(async () => voice().holdStart());
    expect(kind()).toBe('listening');
    expect(screen.getByTestId(listeningPartialId)).toHaveTextContent('set this');
    expect(recognizer.starts).toEqual([{ hotwords: ['SHIP-906', 'Active'] }]);

    await act(async () => {
      jest.advanceTimersByTime(900);
    });
    await act(async () => voice().holdEnd());

    expect(model.parsed.map((p) => p.transcript.text)).toEqual([
      'set this to Active and add a note: fine for v1',
    ]);
    // The model gets the screen's current spec with the transcript (amendment 25).
    expect(model.parsed[0]?.spec).toBe(workItemSpec);
    expect(screen.getByTestId(previewSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(previewLineId('setState'))).toHaveTextContent(/^State: New → Active/);
    expect(screen.getByTestId(previewLineId('addNote'))).toHaveTextContent(/^Note on SHIP-906/);
    expect(internals().last()?.calls).toEqual([
      { action: 'setState', args: {} },
      { action: 'addNote', args: {} },
    ]);
  });

  it('a short press is tooShort, and the mic is read again before a hold', async () => {
    const { recognizer, model, mic } = await mount();
    await hold(200);
    expect(probe.states).toContainEqual({
      kind: 'failure',
      failure: { kind: 'tooShort', detail: { heldMs: 200 } },
      heard: null,
      source: 'voice',
    });
    expect(recognizer.cancelled).toBe(1);
    expect(model.parsed).toEqual([]);
    // A tap is a toast and no sheet (voice D2): the toast closes the failure.
    expect(screen.getByTestId(primitiveIds.toast)).toHaveTextContent(tooShortToast().text);
    expect(kind()).toBe('idle');

    mic.set('deniedFinal');
    await act(async () => voice().holdStart());
    expect(internals().state()).toEqual({
      kind: 'disabled',
      reason: 'micOff',
      permission: 'deniedFinal',
    });
    expect(recognizer.cancelled).toBe(2);
  });

  it('the mic is read again when the app comes back to the front', async () => {
    const listeners: ((s: AppStateStatus) => void)[] = [];
    jest.spyOn(AppState, 'addEventListener').mockImplementation((_type, cb) => {
      listeners.push(cb as (s: AppStateStatus) => void);
      return { remove: () => {} };
    });
    const { mic } = await mount();
    expect(kind()).toBe('idle');

    mic.set('deniedCanAsk');
    await act(async () => {
      for (const cb of listeners) cb('active');
    });
    expect(kind()).toBe('disabled');
    expect(internals().state()).toMatchObject({ reason: 'micOff' });
  });

  it('an answer view shows its lines with Done and dispatches nothing; a navigate view opens its link; a screen view calls onView', async () => {
    const onView = jest.fn();
    const { model, runner } = await mount(onView);

    model.respond([{ action: 'howMany', args: {} }]);
    await hold();
    expect(screen.getByTestId(answerSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(answerLeadId)).toHaveTextContent('Two Features need you');
    expect(screen.getByTestId(answerRowId('SHIP-102'))).toHaveTextContent('SHIP-102Search2 asks');
    await fireEvent.press(screen.getByTestId(answerDoneId));
    expect(kind()).toBe('idle');

    model.respond([{ action: 'open', args: {} }]);
    await hold();
    expect(router.push).toHaveBeenCalledWith('/p/SHIP/items/SHIP-102');
    expect(kind()).toBe('idle');

    model.respond([{ action: 'show', args: {} }]);
    await hold();
    expect(onView).toHaveBeenCalledWith(VIEWS.show);
    expect(kind()).toBe('idle');
    expect(runner.dispatched).toEqual([]);
  });

  it('routes Fix, a fixing hold, submitTyped, a conflict and a failed batch to their flows', async () => {
    const { recognizer, model, runner } = await mount();
    model.respond([{ action: 'setState', args: {} }]);
    await hold();
    expect(kind()).toBe('preview');

    await act(async () => voice().fix());
    expect(startFix).toHaveBeenCalledWith(internals());

    // A hold while fixing goes to fixHeard with the new words; the model isn't asked.
    const preview = internals().state();
    if (preview.kind !== 'preview') throw new Error('expected the preview');
    await act(async () => internals().set({ ...preview, fixing: true }));
    recognizer.script([], 'to Done');
    await hold();
    expect(fixHeard).toHaveBeenCalledWith(internals(), aTranscript({ text: 'to Done' }));
    expect(model.parsed).toHaveLength(1);

    await act(async () => voice().submitTyped('set this to Done'));
    expect(submitTyped).toHaveBeenCalledWith(internals(), 'set this to Done');

    const lines: PreviewLine[] = [{ id: 'l1', icon: 'flag', text: 'State', removable: true }];
    await act(async () => runner.emitConflict('batch-7', lines));
    expect(onConflict).toHaveBeenCalledWith(internals(), 'batch-7', lines, expect.any(Set));

    // Apply sends batch-1. A conflict part is the conflict flow's, and another origin's batch isn't ours.
    await act(async () => internals().set({ ...preview, fixing: false }));
    await act(async () => voice().apply());
    expect(runner.dispatched.map((d) => d.batchId)).toEqual(['batch-1']);
    await act(async () =>
      runner.setPending([
        aPending({ id: 'c', batchId: 'batch-1', state: 'failed', failure: 'conflict' }),
        aPending({ id: 'x', batchId: 'batch-9', state: 'failed', failure: 'server' }),
      ]),
    );
    expect(onApplyFailed).not.toHaveBeenCalled();

    const failed = aPending({ id: 'p1', batchId: 'batch-1', state: 'failed', failure: 'server' });
    await act(async () => runner.setPending([failed]));
    await act(async () => runner.setPending([failed]));
    expect(onApplyFailed).toHaveBeenCalledTimes(1);
    expect(onApplyFailed).toHaveBeenCalledWith(internals(), 'batch-1', [failed]);
  });

  it('an uncertain part of a sent batch goes to onApplyFailed', async () => {
    const { model, runner } = await mount();
    model.respond([{ action: 'setState', args: {} }]);
    await hold();
    await act(async () => voice().apply());
    const unsure = aPending({ id: 'p1', batchId: 'batch-1', state: 'uncertain', origin: 'voice' });
    await act(async () => runner.setPending([unsure]));
    expect(onApplyFailed).toHaveBeenCalledWith(internals(), 'batch-1', [unsure]);
  });
  it('a sent batch whose parts have all left pending() is forgotten', async () => {
    const { model, runner } = await mount();
    model.respond(SET_STATE);
    await hold();
    await act(async () => voice().apply());
    const sending = aPending({ id: 'p1', batchId: 'batch-1', state: 'sending', origin: 'voice' });
    await act(async () => runner.setPending([sending]));
    await act(async () => runner.setPending([]));

    // Forgotten: the same id again is not a batch this provider is watching.
    const failed = aPending({ id: 'p1', batchId: 'batch-1', state: 'failed', failure: 'server' });
    await act(async () => runner.setPending([failed]));
    expect(onApplyFailed).not.toHaveBeenCalled();
  });

  it('a rejected parse or prepare gives a failure, and the next hold reaches the preview', async () => {
    const { model } = await mount();
    model.respond(SET_STATE);
    jest.spyOn(model, 'prepare').mockRejectedValueOnce(new Error('The model failed to load.'));
    await hold();
    expect(internals().state()).toEqual({
      kind: 'failure',
      failure: { kind: 'unclear', detail: { heard: 'do it' } },
      heard: 'do it',
      source: 'voice',
    });

    await act(async () => voice().dismiss());
    jest.spyOn(model, 'parse').mockRejectedValueOnce(new Error('The grammar failed.'));
    await hold();
    expect(internals().state()).toMatchObject({
      kind: 'failure',
      failure: { kind: 'unclear', detail: { heard: 'do it' } },
    });

    await act(async () => voice().dismiss());
    await hold();
    expect(kind()).toBe('preview');
    expect(screen.getByTestId(previewLineId('setState'))).toBeOnTheScreen();
    // The rejected prepare was dropped from the cache and prepared again.
    expect(model.prepared).toEqual(['SHIP-906 Voice commands']);
  });

  it('a rejected release never leaves listening: nothingHeard, or the open preview back', async () => {
    const { recognizer, model } = await mount();
    model.respond(SET_STATE);
    jest.spyOn(recognizer, 'start').mockResolvedValueOnce(brokenSession());
    await hold();
    expect(probe.states).toContainEqual({
      kind: 'failure',
      failure: { kind: 'nothingHeard', detail: {} },
      heard: null,
      source: 'voice',
    });
    // A silent hold is a toast and no sheet (voice D1): the toast closes the failure, never back to listening.
    expect(kind()).toBe('idle');

    await hold();
    const preview = internals().state();
    expect(preview.kind).toBe('preview');

    jest.spyOn(recognizer, 'start').mockResolvedValueOnce(brokenSession());
    await hold();
    expect(internals().state()).toEqual(preview);
  });

  it('a rejected mic read on a hold drops the hold instead of leaving it listening', async () => {
    const { recognizer, mic } = await mount();
    jest.spyOn(mic, 'status').mockRejectedValueOnce(new Error('The permission check failed.'));
    await act(async () => voice().holdStart());
    expect(kind()).toBe('idle');
    expect(recognizer.cancelled).toBe(1);
  });

  it('prepares once across three holds while the screen rebuilds its spec on every render', async () => {
    const { model } = await mount(undefined, true);
    model.respond(SET_STATE);
    const specs = new Set<ScreenVoiceSpec | null>();
    for (let i = 0; i < 3; i++) {
      specs.add(internals().spec());
      await hold();
      expect(kind()).toBe('preview');
    }
    // Each hold re-rendered the screen, which registered a new spec object.
    expect(specs.size).toBe(3);
    expect(model.parsed).toHaveLength(3);
    expect(model.prepared).toHaveLength(1);
  });

  it('Apply after a resolveCalls rebuild sends the rebuilt actions', async () => {
    const { model, runner } = await mount();
    model.respond(SET_STATE);
    await hold();
    const preview = internals().state();
    if (preview.kind !== 'preview') throw new Error('expected the preview');

    // A flow (Fix, a conflict) resolves the merged calls again and shows the rebuilt preview.
    const rebuilt = internals().resolveCalls(
      [{ action: 'addNote', args: {} }],
      aTranscript({ text: 'add a note' }),
    );
    if (!rebuilt.ok) throw new Error('expected a resolved command');
    await act(async () => internals().set({ ...preview, lines: rebuilt.lines }));
    await act(async () => voice().apply());

    expect(runner.dispatched.map((d) => d.batch)).toEqual([[action('addNote')]]);
  });

  it('"yes" with every line removed sends nothing', async () => {
    const { recognizer, model, runner } = await mount();
    model.respond(SET_STATE);
    await hold();
    const preview = internals().state();
    if (preview.kind !== 'preview') throw new Error('expected the preview');
    await act(async () => internals().set({ ...preview, removed: ['setState'] }));

    recognizer.script([], 'yes');
    await hold();
    expect(runner.dispatched).toEqual([]);
    expect(kind()).toBe('preview');
  });

  it('while fixing, "yes" goes to fixHeard and applies nothing', async () => {
    const { recognizer, model, runner } = await mount();
    model.respond(SET_STATE);
    await hold();
    const preview = internals().state();
    if (preview.kind !== 'preview') throw new Error('expected the preview');
    await act(async () => internals().set({ ...preview, fixing: true }));

    recognizer.script([], 'yes');
    await hold();
    expect(fixHeard).toHaveBeenCalledWith(internals(), aTranscript({ text: 'yes' }));
    expect(runner.dispatched).toEqual([]);
  });
});
