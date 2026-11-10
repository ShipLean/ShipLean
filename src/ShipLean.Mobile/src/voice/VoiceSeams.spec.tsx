import { act, fireEvent, screen } from '@testing-library/react-native';
import { useEffect, useState, type ReactNode } from 'react';
import { NavigationContext } from 'expo-router/react-navigation';
import { Pressable, Text } from 'react-native';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import type { ActionRunner, PreviewLine } from '../actions/types';
import { UndoToastHost } from '../actions/UndoToastHost';
import { undoToastId } from '../actions/UndoToastHost.testIds';
import { RunnerProvider } from '../shell/providers/runner';
import { aPending, fakeRunner } from '../test-support/actions';
import { renderScreen } from '../test-support/renderScreen';
import { renderWithRunner } from '../test-support/renderWithRunner';
import { installFakeEngines, renderWithVoice } from '../test-support/voiceUi';
import { primitiveIds } from '../ui/testIds';
import { ToastHost } from '../ui/Toast';
import { VoiceChangeRow } from './after/VoiceChangeRow';
import { voiceChangeRowId } from './after/VoiceChangeRow.testIds';
import { VoiceSendFailedBanner } from './after/VoiceSendFailedBanner';
import { voiceSendFailedId } from './after/VoiceSendFailedBanner.testIds';
import type { ScreenVoiceSpec, VoiceFailure } from './contracts';
import { CorrectionFailure } from './failures/CorrectionFailure';
import { correctionFailureId } from './failures/CorrectionFailure.testIds';
import { HeardFailure } from './failures/HeardFailure';
import { heardFailureId } from './failures/HeardFailure.testIds';
import { WhatCanISay } from './failures/WhatCanISay';
import { whatCanISayId } from './failures/WhatCanISay.testIds';
import { onConflict } from './flows/conflict';
import { startFix } from './flows/fix';
import { retryWith } from './flows/retryWith';
import { submitTyped } from './flows/typed';
import { useVoiceInternals, type VoiceInternals } from './internals';
import { MicSlot } from './MicSlot';
import { AnswerSheet } from './sheets/AnswerSheet';
import { answerSheetId } from './sheets/AnswerSheet.testIds';
import { ApplyFailedSheet } from './sheets/ApplyFailedSheet';
import { applyFailedSheetId } from './sheets/ApplyFailedSheet.testIds';
import { HeardQuote } from './sheets/HeardQuote';
import { ListeningSheet } from './sheets/ListeningSheet';
import { listeningSheetId } from './sheets/ListeningSheet.testIds';
import { MicPermissionSheet } from './sheets/MicPermissionSheet';
import { micPermissionSheetId } from './sheets/MicPermissionSheet.testIds';
import { PreviewSheet } from './sheets/PreviewSheet';
import { previewSheetId } from './sheets/PreviewSheet.testIds';
import { VoiceFailureSheet } from './sheets/VoiceFailureSheet';
import { voiceFailureSheetId } from './sheets/VoiceFailureSheet.testIds';
import { CommandComposer } from './typed/CommandComposer';
import { commandComposerId } from './typed/CommandComposer.testIds';
import { useScreenVoice, useVoice, type VoiceApi } from './VoiceProvider';

jest.mock('./flows/fix', () => ({ startFix: jest.fn(), fixHeard: jest.fn() }));
jest.mock('./flows/typed', () => ({ submitTyped: jest.fn(async () => {}) }));
jest.mock('./flows/retryWith', () => ({ retryWith: jest.fn(async () => {}) }));
jest.mock('./flows/conflict', () => ({ onConflict: jest.fn() }));

// The real failure views plus a press that asks "What can I say?", so the router's swap can be driven.
function mockWithWhatCanISay(path: string, name: string) {
  const { createElement, Fragment } = jest.requireActual<typeof import('react')>('react');
  const { Pressable: Press } = jest.requireActual<typeof import('react-native')>('react-native');
  const Actual = jest.requireActual(path)[name];
  return {
    ...jest.requireActual(path),
    [name]: (props: { onWhatCanISay: () => void }) =>
      createElement(
        Fragment,
        null,
        createElement(Actual, props),
        createElement(Press, { testID: 'ask-what-can-i-say', onPress: props.onWhatCanISay }),
      ),
  };
}
jest.mock('./failures/HeardFailure', () =>
  mockWithWhatCanISay('./failures/HeardFailure', 'HeardFailure'),
);
jest.mock('./failures/CorrectionFailure', () =>
  mockWithWhatCanISay('./failures/CorrectionFailure', 'CorrectionFailure'),
);

let mockSession: { origin: string | null; email: string } = {
  origin: 'https://ship.test',
  email: 'tom@ship.test',
};
jest.mock('../session/useSession', () => ({ useSession: () => mockSession }));

const NOW = new Date('2030-01-01T09:00:00.000Z');
const noop = () => {};

// What the probes saw on their last render.
const probe: { voice: VoiceApi | null; internals: VoiceInternals | null } = {
  voice: null,
  internals: null,
};
function VoiceProbe() {
  const voice = useVoice();
  useEffect(() => {
    probe.voice = voice;
  });
  return <Text testID="voice-state">{JSON.stringify(voice.state)}</Text>;
}
function InternalsProbe() {
  const internals = useVoiceInternals();
  useEffect(() => {
    probe.internals = internals;
  });
  return null;
}
const voice = () => probe.voice!;
const internals = () => probe.internals!;
const shownState = () => JSON.parse(screen.getByTestId('voice-state').props.children as string);

const spec: ScreenVoiceSpec = {
  screen: 'backlog',
  actions: [],
  context: () => ({}),
  hotwords: () => [],
  toActions: () => ({ ok: true, actions: [], lines: [], rejected: [], view: null }),
};

beforeEach(() => {
  jest.useFakeTimers({ now: NOW });
  mockSession = { origin: 'https://ship.test', email: 'tom@ship.test' };
  jest.clearAllMocks();
});
afterEach(() => jest.useRealTimers());

describe('voice seams', () => {
  it('each stub renders its root id from its props', async () => {
    const failure: VoiceFailure = { kind: 'nothingHeard', detail: {} };
    const line: PreviewLine = {
      id: 'l1',
      icon: 'dot',
      text: 'Move SHIP-1 to Done',
      removable: true,
    };
    // The voice change row and the refused-change banner read the runner.
    await renderWithRunner(
      <ToastHost>
        <MicSlot placement="tabBar" />
        <MicSlot placement="dock" />
        <ListeningSheet
          state={{ kind: 'listening', partial: 'move', screen: 'backlog', hotwords: [] }}
        />
        <PreviewSheet
          state={{
            kind: 'preview',
            heard: 'move it',
            source: 'voice',
            lines: [line],
            removed: [],
            marks: [],
            fixing: false,
          }}
          onApply={noop}
          onFix={noop}
          onCancel={noop}
        />
        <AnswerSheet content={{ lead: 'Two', rows: [] }} onClose={noop} />
        <ApplyFailedSheet
          state={{
            kind: 'applyFailed',
            batchId: 'b1',
            heard: 'move it',
            source: 'voice',
            lines: [line],
            reason: 'No connection',
            retryable: true,
          }}
          onRetry={noop}
          onQueue={noop}
          onDiscard={noop}
        />
        <HeardFailure
          failure={failure}
          heard={null}
          source="voice"
          onRetry={noop}
          onTypeInstead={noop}
          onWhatCanISay={noop}
          onClose={noop}
        />
        <CorrectionFailure
          failure={{ kind: 'webOnly', detail: { action: 'split', where: 'the web' } }}
          heard="split it"
          source="voice"
          onRetry={noop}
          onWhatCanISay={noop}
          onClose={noop}
        />
        <WhatCanISay spec={spec} onClose={noop} />
        <HeardQuote text="move it" source="typed" misses={[]} testID="quote-a" />
        <CommandComposer text="" onChangeText={noop} reason="micOff" onSent={noop} />
        <VoiceChangeRow itemId="i1" itemKey="SHIP-1" />
        <VoiceSendFailedBanner />
        <MicPermissionSheet open status="deniedFinal" onClose={noop} onTypeInstead={noop} />
        <MicPermissionSheet open={false} status="granted" onClose={noop} onTypeInstead={noop} />
      </ToastHost>,
    );
    for (const id of [
      'mic-tabBar',
      'mic-dock',
      listeningSheetId,
      previewSheetId,
      answerSheetId,
      applyFailedSheetId,
      heardFailureId,
      correctionFailureId,
      whatCanISayId,
      'quote-a',
      commandComposerId,
      voiceChangeRowId,
      voiceSendFailedId,
    ]) {
      expect(screen.getByTestId(id)).toBeOnTheScreen();
    }
    // The closed permission sheet renders nothing.
    expect(screen.getAllByTestId(micPermissionSheetId)).toHaveLength(1);
  });

  it('useVoice outside a provider is disabled with noModels and its calls do nothing', async () => {
    await renderScreen(<VoiceProbe />);
    expect(shownState()).toEqual({
      kind: 'disabled',
      reason: 'noModels',
      permission: 'undetermined',
    });
    await act(async () => {
      voice().holdStart();
      voice().holdEnd();
      voice().apply(['l1']);
      voice().fix();
      voice().submitTyped('move it');
      voice().retryWith({ asNote: true });
      voice().typeInstead();
      voice().cancel();
      voice().dismiss();
    });
    expect(shownState()).toEqual({
      kind: 'disabled',
      reason: 'noModels',
      permission: 'undetermined',
    });
    expect(startFix).not.toHaveBeenCalled();
    expect(submitTyped).not.toHaveBeenCalled();
    expect(retryWith).not.toHaveBeenCalled();
  });

  it('the provider is disabled with downloading while the model store downloads, and idle once ready with the mic granted', async () => {
    const engines = installFakeEngines();
    engines.models.set({ phase: 'downloading', bytesDone: 10, bytesTotal: 100 });
    await renderWithVoice(<VoiceProbe />, { engines });
    await act(async () => {});
    expect(shownState()).toEqual({
      kind: 'disabled',
      reason: 'downloading',
      permission: 'granted',
    });

    await act(async () => engines.models.set({ phase: 'ready' }));
    expect(shownState()).toEqual({ kind: 'idle', permission: 'granted' });
  });

  it('a ready model with a denied mic is disabled with micOff', async () => {
    const engines = installFakeEngines();
    engines.mic.set('deniedFinal');
    await renderWithVoice(<VoiceProbe />, { engines });
    await act(async () => {});
    expect(shownState()).toEqual({
      kind: 'disabled',
      reason: 'micOff',
      permission: 'deniedFinal',
    });
  });

  it('useScreenVoice registers the focused screen spec and drops it on unmount', async () => {
    function Screen() {
      useScreenVoice(spec);
      return null;
    }
    function Host() {
      const [shown, setShown] = useState(true);
      return (
        <>
          {shown ? <Screen /> : null}
          <Pressable testID="leave" onPress={() => setShown(false)} />
          <InternalsProbe />
        </>
      );
    }
    await renderWithVoice(<Host />, { engines: {} });
    expect(internals().spec()).toBe(spec);

    await fireEvent.press(screen.getByTestId('leave'));
    expect(internals().spec()).toBeNull();
  });

  describe('useScreenVoice follows focus', () => {
    const specOf = (screenName: ScreenVoiceSpec['screen']): ScreenVoiceSpec => ({
      ...spec,
      screen: screenName,
    });
    const inbox = specOf('inbox');
    const backlog = specOf('backlog');

    // A minimal navigation prop: focus and blur listeners, and which of them is focused now.
    function fakeNav(focused: boolean) {
      const listeners: Record<string, Set<() => void>> = { focus: new Set(), blur: new Set() };
      const nav = {
        focused,
        isFocused: () => nav.focused,
        addListener: (type: 'focus' | 'blur', cb: () => void) => {
          listeners[type]!.add(cb);
          return () => {
            listeners[type]!.delete(cb);
          };
        },
        emit(type: 'focus' | 'blur') {
          nav.focused = type === 'focus';
          for (const cb of [...listeners[type]!]) cb();
        },
      };
      return nav;
    }
    type FakeNav = ReturnType<typeof fakeNav>;

    function Screen({ nav, of }: { nav: FakeNav; of: ScreenVoiceSpec }) {
      return (
        <NavigationContext.Provider value={nav as never}>
          <Inner of={of} />
        </NavigationContext.Provider>
      );
    }
    function Inner({ of }: { of: ScreenVoiceSpec }) {
      useScreenVoice(of);
      return null;
    }
    const hostRef: { rerender: (of: ScreenVoiceSpec) => void } = { rerender: noop };
    function BackgroundHost({ nav }: { nav: FakeNav }) {
      const [of, setOf] = useState(backlog);
      useEffect(() => {
        hostRef.rerender = setOf;
      }, []);
      return <Screen nav={nav} of={of} />;
    }

    it('two mounted screens: the newest wins, and a focus change moves the mic back', async () => {
      const first = fakeNav(true);
      const second = fakeNav(false);
      await renderWithVoice(
        <>
          <Screen nav={first} of={inbox} />
          <Screen nav={second} of={backlog} />
          <InternalsProbe />
        </>,
        { engines: {} },
      );
      // Only the focused screen has registered.
      expect(internals().spec()).toBe(inbox);

      await act(async () => {
        first.emit('blur');
        second.emit('focus');
      });
      expect(internals().spec()).toBe(backlog);

      await act(async () => {
        second.emit('blur');
        first.emit('focus');
      });
      expect(internals().spec()).toBe(inbox);

      await act(async () => first.emit('blur'));
      expect(internals().spec()).toBeNull();
    });

    it('a background screen that re-renders with a new spec does not take the mic', async () => {
      const visible = fakeNav(true);
      const background = fakeNav(false);
      await renderWithVoice(
        <>
          <Screen nav={visible} of={inbox} />
          <BackgroundHost nav={background} />
          <InternalsProbe />
        </>,
        { engines: {} },
      );
      expect(internals().spec()).toBe(inbox);
      await act(async () => hostRef.rerender(specOf('retro')));
      expect(internals().spec()).toBe(inbox);
    });

    it('an older screen that re-renders keeps its place under the newer one, with its spec current', async () => {
      const older = fakeNav(true);
      const newer = fakeNav(true);
      await renderWithVoice(
        <>
          <BackgroundHost nav={older} />
          <Screen nav={newer} of={inbox} />
          <InternalsProbe />
        </>,
        { engines: {} },
      );
      expect(internals().spec()).toBe(inbox);
      const retro = specOf('retro');
      await act(async () => hostRef.rerender(retro));
      expect(internals().spec()).toBe(inbox);

      await act(async () => newer.emit('blur'));
      expect(internals().spec()).toBe(retro);
    });
  });

  it('fix, submitTyped, retryWith and a reported conflict reach their flows', async () => {
    const runner = fakeRunner();
    await renderWithVoice(
      <>
        <VoiceProbe />
        <InternalsProbe />
      </>,
      { engines: {}, runner },
    );
    const lines: PreviewLine[] = [{ id: 'l1', icon: 'dot', text: 'Move', removable: true }];
    await act(async () => {
      voice().fix();
      voice().submitTyped('move SHIP-1 to done');
      voice().retryWith({ span: { start: 5, end: 11 }, value: 'SHIP-2' });
      runner.emitConflict('batch-9', lines);
    });
    expect(startFix).toHaveBeenCalledWith(internals());
    expect(submitTyped).toHaveBeenCalledWith(internals(), 'move SHIP-1 to done');
    expect(retryWith).toHaveBeenCalledWith(internals(), {
      span: { start: 5, end: 11 },
      value: 'SHIP-2',
    });
    expect(onConflict).toHaveBeenCalledWith(internals(), 'batch-9', lines);
  });

  it('the failure router sends each kind to its sheet', async () => {
    const heardKinds: VoiceFailure[] = [
      { kind: 'tooShort', detail: { heldMs: 200 } },
      { kind: 'nothingHeard', detail: {} },
      { kind: 'unclear', detail: { heard: 'um' } },
    ];
    const correctionKinds: VoiceFailure[] = [
      { kind: 'noMatch', detail: { spoken: 'ship nine', role: 'target item', span: null } },
      { kind: 'misheardKey', detail: { spoken: 'SHIP-960', suggestion: 'SHIP-906' } },
      { kind: 'noValidAction', detail: { action: 'move', reason: 'noSuchState' } },
      { kind: 'webOnly', detail: { action: 'split', where: 'the web' } },
      {
        kind: 'ambiguous',
        detail: { candidates: [], call: { action: 'move', args: {} } },
      },
    ];
    const sheet = (failure: VoiceFailure) => (
      <VoiceFailureSheet
        key={failure.kind}
        state={{ kind: 'failure', failure, heard: 'move it', source: 'voice' }}
        spec={spec}
        onRetry={noop}
        onTypeInstead={noop}
        onClose={noop}
      />
    );
    for (const failure of [...heardKinds, ...correctionKinds]) {
      const view = await renderScreen(<ToastHost>{sheet(failure)}</ToastHost>);
      const heard = heardKinds.includes(failure);
      expect(screen.getByTestId(voiceFailureSheetId)).toBeOnTheScreen();
      expect(screen.queryByTestId(heardFailureId) !== null).toBe(heard);
      expect(screen.queryByTestId(correctionFailureId) !== null).toBe(!heard);

      await fireEvent.press(screen.getByTestId('ask-what-can-i-say'));
      expect(screen.getByTestId(whatCanISayId)).toBeOnTheScreen();
      expect(screen.queryByTestId(heardFailureId)).toBeNull();
      expect(screen.queryByTestId(correctionFailureId)).toBeNull();
      await view.unmount();
    }
  });

  it('UndoToastHost shows toast-undo for a batch in its window and Undo undoes it', async () => {
    const runner = fakeRunner();
    const until = new Date(NOW.getTime() + 8000).toISOString();
    runner.setPending([
      aPending({
        id: 'p1',
        batchId: 'b-old',
        createdAt: new Date(NOW.getTime() - 60_000).toISOString(),
        undoUntil: null,
      }),
      aPending({ id: 'p2', batchId: 'b-new', createdAt: NOW.toISOString(), undoUntil: until }),
    ]);
    await renderWithRunner(<UndoToastHost />, { runner });
    expect(screen.getByTestId(undoToastId)).toBeOnTheScreen();

    await fireEvent.press(screen.getByTestId(primitiveIds.toastAction));
    expect(runner.undone).toEqual(['b-new']);
    expect(screen.queryByTestId(undoToastId)).toBeNull();
  });

  it('the runner slot builds one runner per origin and email and a new one for another account', async () => {
    const seen: ActionRunner[] = [];
    function Probe() {
      seen.push(useActionRunner());
      return null;
    }
    let bump = () => {};
    function Host({ children }: { children: ReactNode }) {
      const [, setN] = useState(0);
      useEffect(() => {
        bump = () => setN((n) => n + 1);
      }, []);
      return <RunnerProvider projectKey="SHIP">{children}</RunnerProvider>;
    }
    await renderScreen(
      <Host>
        <Probe />
      </Host>,
    );
    await act(async () => bump());
    expect(new Set(seen).size).toBe(1);

    mockSession = { origin: 'https://ship.test', email: 'ann@ship.test' };
    await act(async () => bump());
    expect(new Set(seen).size).toBe(2);

    mockSession = { origin: 'https://other.test', email: 'ann@ship.test' };
    await act(async () => bump());
    expect(new Set(seen).size).toBe(3);
  });
});
