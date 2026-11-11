// The flows through the real VoiceProvider: what the flow tests' harness can't show, because the provider's settle and
// the runner's order of events decide which flow runs.
import { ApiRequestError } from '@shiplean/core/api/client';
import { memoryStore } from '@shiplean/core/platform';
import { act, fireEvent, screen, within } from '@testing-library/react-native';
import { useEffect, useState, type ReactNode } from 'react';
import { systemNow } from '../../actions/clock';
import { createActionRunner, type SessionRunner } from '../../actions/runner';
import type { Executor } from '../../actions/types';
import { UNDO } from '../../actions/undo';
import { FakeCommandModel, FakeRecognizer } from '../../test-support/voice';
import { flowSpec, type FlowAction, type FlowContext } from '../../test-support/voiceFlows';
import { renderWithVoice } from '../../test-support/voiceUi';
import { VoiceSendFailedBanner } from '../after/VoiceSendFailedBanner';
import { voiceSendFailedRefusedId } from '../after/VoiceSendFailedBanner.testIds';
import type { ParsedCall, ScreenVoiceSpec } from '../contracts';
import { heardFailureNoteId } from '../failures/HeardFailure.testIds';
import {
  applyFailedGroupId,
  applyFailedHeadingId,
  applyFailedLineId,
  applyFailedRetryId,
  applyFailedSheetId,
} from '../sheets/ApplyFailedSheet.testIds';
import { previewApplyId, previewMarkId, previewSheetId } from '../sheets/PreviewSheet.testIds';
import { useScreenVoice, useVoice, VoiceSheetsHost, type VoiceApi } from '../VoiceProvider';
import { CHANGED_HERE } from './conflict';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const HEARD = 'set it to active and approve D10';
const CALLS: ParsedCall[] = [
  { action: 'set_state', args: { state: 'Active' } },
  { action: 'approve', args: { number: 'D10' } },
];

const probe: { voice: VoiceApi | null } = { voice: null };
function Screen({ spec, children }: { spec: ScreenVoiceSpec<FlowAction>; children?: ReactNode }) {
  useScreenVoice(spec as ScreenVoiceSpec);
  const voice = useVoice();
  useEffect(() => {
    probe.voice = voice;
  });
  return (
    <>
      {children}
      <VoiceSheetsHost />
    </>
  );
}
const voice = () => probe.voice!;

// The work item screen: a refetch renders it again with the server's item.
const refetch: { now: (() => void) | null } = { now: null };
function Refetching({ spec }: { spec: ScreenVoiceSpec<FlowAction> }) {
  const [, setN] = useState(0);
  useEffect(() => {
    refetch.now = () => setN((n) => n + 1);
  });
  return <Screen spec={spec} />;
}

// The real runner, started, with a work item executor that refuses setState with a 409 (a stale revision).
function staleRunner() {
  const sent: string[] = [];
  const executor: Executor<FlowAction> = {
    area: 'workItem',
    send: async (a) => {
      sent.push(a.kind);
      if (a.kind === 'setState') throw new ApiRequestError('Stale revision.', 409, 'conflict');
    },
    describe: (a) => ({ id: a.kind, icon: 'dot', text: a.kind, removable: true }),
    reversible: () => true,
    undo: async () => {},
  };
  const runner = createActionRunner({
    store: memoryStore(),
    scope: 'https://ship.test\ntom@ship.test',
    isOffline: () => false,
    onOnline: () => () => {},
    now: systemNow,
  });
  runner.register(executor);
  runner.start();
  return { runner, sent };
}

// The real runner, started, over a connection the test switches: its work item executor refuses approve with
// `refusal` while `refusing.approve` is true, and logs every send, undo and read of the screen's context (a resolve) in
// order.
function switchedRunner(refusal: ApiRequestError, offline: boolean) {
  const log: string[] = [];
  const refusing = { approve: true };
  const connection = { offline, online: new Set<() => void>() };
  const executor: Executor<FlowAction> = {
    area: 'workItem',
    send: async (a) => {
      log.push(`send ${a.kind}`);
      if (a.kind === 'approve' && refusing.approve) throw refusal;
    },
    describe: (a) => ({ id: a.kind, icon: 'dot', text: a.kind, removable: true }),
    reversible: (kind) => kind !== 'approve',
    undo: async (a) => {
      log.push(`undo ${a.kind}`);
    },
  };
  const runner = createActionRunner({
    store: memoryStore(),
    scope: 'https://ship.test\ntom@ship.test',
    isOffline: () => connection.offline,
    onOnline: (cb) => {
      connection.online.add(cb);
      return () => connection.online.delete(cb);
    },
    now: systemNow,
  });
  runner.register(executor);
  runner.start();
  const goOnline = async () => {
    connection.offline = false;
    await act(async () => {
      for (const cb of [...connection.online]) cb();
      await jest.advanceTimersByTimeAsync(0);
    });
  };
  return { runner, log, goOnline, refusing };
}

// Speaks HEARD on a work item screen over `runner`, and applies the preview. `log` hears each read of the context.
async function speakAndApply(runner: SessionRunner, log: string[] = [], children?: ReactNode) {
  const context: FlowContext = { key: 'SHIP-906', state: 'New', revision: 4, decisions: ['D10'] };
  const spec = flowSpec(() => context);
  const logged: ScreenVoiceSpec<FlowAction> = {
    ...spec,
    toActions: (calls, t) => {
      log.push('resolve');
      return spec.toActions(calls, t);
    },
  };
  const recognizer = new FakeRecognizer();
  const model = new FakeCommandModel();
  model.respond(CALLS);
  await renderWithVoice(<Screen spec={logged}>{children}</Screen>, {
    engines: { recognizer, model },
    runner,
  });
  recognizer.script([], HEARD);
  await act(async () => voice().holdStart());
  await act(async () => {
    jest.advanceTimersByTime(900);
  });
  await act(async () => voice().holdEnd());
  log.length = 0;
  await fireEvent.press(screen.getByTestId(previewApplyId));
  await act(async () => {
    await jest.advanceTimersByTimeAsync(0);
  });
}

const partOf = (runner: SessionRunner, kind: string) =>
  runner.pending().find((p) => p.action.kind === kind)!;

let started: SessionRunner | null = null;
beforeEach(() => jest.useFakeTimers({ now: NOW }));
afterEach(() => {
  started?.stop();
  started = null;
  jest.useRealTimers();
});

describe('voice flows through the provider', () => {
  it('a 409 on a two-action voice batch ends on the re-confirm preview, not the apply-failed sheet', async () => {
    const context: FlowContext = {
      key: 'SHIP-906',
      state: 'New',
      revision: 4,
      decisions: ['D10'],
    };
    const { runner, sent } = staleRunner();
    started = runner;
    const recognizer = new FakeRecognizer();
    const model = new FakeCommandModel();
    model.respond(CALLS);
    await renderWithVoice(<Screen spec={flowSpec(() => context)} />, {
      engines: { recognizer, model },
      runner,
    });

    recognizer.script([], HEARD);
    await act(async () => voice().holdStart());
    await act(async () => {
      jest.advanceTimersByTime(900);
    });
    await act(async () => voice().holdEnd());
    expect(screen.getByTestId(previewSheetId)).toBeOnTheScreen();

    // Priya resolved it meanwhile: the server refuses the stale state change.
    context.state = 'Resolved';
    context.revision = 5;
    await fireEvent.press(screen.getByTestId(previewApplyId));
    await act(async () => {
      await jest.advanceTimersByTimeAsync(0);
    });

    expect(sent).toEqual(['setState']);
    expect(voice().state).toMatchObject({ kind: 'preview', source: 'voice' });
    expect(screen.queryByTestId(applyFailedSheetId)).toBeNull();
    expect(screen.getByTestId(previewSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(previewMarkId('state'))).toHaveTextContent(CHANGED_HERE);
  });

  it('a 409 the screen has not read yet keeps applying through a runner update, and the preview comes back once it has', async () => {
    const context: FlowContext = { key: 'SHIP-906', state: 'New', revision: 4, decisions: [] };
    const spec = flowSpec(() => context);
    const { runner, sent } = staleRunner();
    started = runner;
    const recognizer = new FakeRecognizer();
    const model = new FakeCommandModel();
    model.respond([{ action: 'set_state', args: { state: 'Active' } }]);
    await renderWithVoice(<Refetching spec={spec} />, { engines: { recognizer, model }, runner });
    recognizer.script([], 'set it to active');
    await act(async () => voice().holdStart());
    await act(async () => {
      jest.advanceTimersByTime(900);
    });
    await act(async () => voice().holdEnd());

    // Refused as stale before the screen has read SHIP-906 again: it waits, applying.
    await fireEvent.press(screen.getByTestId(previewApplyId));
    await act(async () => {
      await jest.advanceTimersByTimeAsync(0);
    });
    expect(sent).toEqual(['setState']);
    expect(voice().state).toMatchObject({ kind: 'applying' });

    // The runner tells its subscribers again meanwhile (a tapped note sends).
    await act(async () => {
      const note: FlowAction = {
        area: 'workItem',
        itemId: 'item-1',
        key: 'SHIP-906',
        kind: 'addNote',
        text: 'hi',
      };
      runner.dispatch([note], { undo: UNDO.typed, origin: 'tap', allOrNothing: false });
      await jest.advanceTimersByTimeAsync(0);
    });
    expect(sent).toEqual(['setState', 'addNote']);
    expect(voice().state).toMatchObject({ kind: 'applying' });

    // The refetch lands: Priya resolved it meanwhile.
    context.state = 'Resolved';
    context.revision = 5;
    await act(async () => refetch.now?.());

    expect(voice().state).toMatchObject({ kind: 'preview', source: 'voice' });
    expect(screen.getByTestId(previewSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(previewMarkId('state'))).toHaveTextContent(CHANGED_HERE);
  });

  it('typed Post as a note retries as a note and applies it with origin typed', async () => {
    const context: FlowContext = { key: 'SHIP-906', state: 'New', revision: 4, decisions: [] };
    const model = new FakeCommandModel();
    // Nothing the model can parse: the typed words come back unclear.
    model.respond([]);
    const view = await renderWithVoice(<Screen spec={flowSpec(() => context)} />, {
      engines: { model },
    });

    await act(async () => voice().submitTyped('the S22 runs warm'));
    expect(voice().state).toMatchObject({ kind: 'failure', source: 'typed' });

    await fireEvent.press(screen.getByTestId(heardFailureNoteId));

    expect(view.runner.dispatched).toEqual([
      {
        batch: [
          {
            area: 'workItem',
            kind: 'addNote',
            itemId: 'item-1',
            key: 'SHIP-906',
            text: 'the S22 runs warm',
          },
        ],
        // Sent by the voice provider: via 'voice', so VoiceSendFailedBanner lists it if it is refused.
        opts: { undo: UNDO.typed, origin: 'typed', allOrNothing: true, via: 'voice' },
        batchId: 'batch-1',
      },
    ]);
  });

  it('a voice batch applied offline and refused once online shows its sent part as still applied', async () => {
    const { runner, log, goOnline } = switchedRunner(
      new ApiRequestError('Refused.', 500, 'internal_error'),
      true,
    );
    started = runner;
    await speakAndApply(runner);
    expect(log).toEqual([]);

    await goOnline();

    // The batch went out after the connection came back, so the runner reversed nothing: the state change stayed.
    expect(log).toEqual(['send setState', 'send approve']);
    expect(screen.getByTestId(applyFailedHeadingId)).toHaveTextContent(
      'Some changes stayed applied',
    );
    expect(
      within(screen.getByTestId(applyFailedGroupId('stillApplied'))).getByTestId(
        applyFailedLineId(partOf(runner, 'setState').id),
      ),
    ).toBeOnTheScreen();
    expect(screen.queryByTestId(applyFailedGroupId('reversing'))).toBeNull();
  });

  it('a refused live voice batch reaches the apply-failed sheet only once the runner has reversed it', async () => {
    const { runner, log } = switchedRunner(
      new ApiRequestError('Refused.', 500, 'internal_error'),
      false,
    );
    started = runner;
    await speakAndApply(runner);

    expect(log).toEqual(['send setState', 'send approve', 'undo setState']);
    // onApplyFailed copied the refused part's reason when it first saw the batch: the reversal's outcome is in it.
    expect(voice().state).toMatchObject({
      kind: 'applyFailed',
      reason: "Nothing changed. Didn't send (500).",
    });
    expect(screen.getByTestId(applyFailedHeadingId)).toHaveTextContent('Nothing was changed');
    expect(
      within(screen.getByTestId(applyFailedGroupId('reversed'))).getByTestId(
        applyFailedLineId(partOf(runner, 'setState').id),
      ),
    ).toBeOnTheScreen();
  });

  it('a 409 on a live voice batch rebuilds the preview only after the runner reversed what was sent', async () => {
    const { runner, log } = switchedRunner(
      new ApiRequestError('Stale revision.', 409, 'conflict'),
      false,
    );
    started = runner;
    await speakAndApply(runner, log);

    // onConflict resolves the calls again: after the reversal, not before it.
    expect(log).toEqual(['send setState', 'send approve', 'undo setState', 'resolve']);
    expect(voice().state).toMatchObject({ kind: 'preview', source: 'voice' });
    expect(screen.queryByTestId(applyFailedSheetId)).toBeNull();
  });

  it('Try again on a reversed voice batch retires its old refused part, so the banner stops showing it', async () => {
    const { runner, log, refusing } = switchedRunner(
      new ApiRequestError('Refused.', 500, 'internal_error'),
      false,
    );
    started = runner;
    await speakAndApply(runner, [], <VoiceSendFailedBanner />);
    const refused = partOf(runner, 'approve').id;
    expect(screen.getByTestId(voiceSendFailedRefusedId(refused))).toBeOnTheScreen();

    refusing.approve = false;
    await fireEvent.press(screen.getByTestId(applyFailedRetryId));
    await act(async () => {
      await jest.advanceTimersByTimeAsync(0);
    });

    expect(log).toEqual([
      'send setState',
      'send approve',
      'undo setState',
      'send setState',
      'send approve',
    ]);
    expect(runner.pending().map((p) => p.id)).not.toContain(refused);
    expect(screen.queryByTestId(voiceSendFailedRefusedId(refused))).toBeNull();
  });
});
