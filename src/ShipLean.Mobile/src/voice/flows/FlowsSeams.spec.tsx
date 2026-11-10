// The flows through the real VoiceProvider: what the flow tests' harness can't show, because the provider's settle and
// the runner's order of events decide which flow runs.
import { ApiRequestError } from '@shiplean/core/api/client';
import { memoryStore } from '@shiplean/core/platform';
import { act, fireEvent, screen } from '@testing-library/react-native';
import { useEffect } from 'react';
import { systemNow } from '../../actions/clock';
import { createActionRunner, type SessionRunner } from '../../actions/runner';
import type { Executor } from '../../actions/types';
import { UNDO } from '../../actions/undo';
import { FakeCommandModel, FakeRecognizer } from '../../test-support/voice';
import { flowSpec, type FlowAction, type FlowContext } from '../../test-support/voiceFlows';
import { renderWithVoice } from '../../test-support/voiceUi';
import type { ParsedCall, ScreenVoiceSpec } from '../contracts';
import { heardFailureNoteId } from '../failures/HeardFailure.testIds';
import { applyFailedSheetId } from '../sheets/ApplyFailedSheet.testIds';
import { previewApplyId, previewMarkId, previewSheetId } from '../sheets/PreviewSheet.testIds';
import { useScreenVoice, useVoice, VoiceSheetsHost, type VoiceApi } from '../VoiceProvider';
import { CHANGED_HERE } from './conflict';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const HEARD = 'set it to active and approve D10';
const CALLS: ParsedCall[] = [
  { action: 'set_state', args: { state: 'Active' } },
  { action: 'approve', args: { number: 'D10' } },
];

const text = { type: 'string' };
// flowSpec with argument schemas, so the provider's resolveCommand passes its calls.
function screenSpec(context: FlowContext): ScreenVoiceSpec<FlowAction> {
  const spec = flowSpec(() => context);
  return {
    ...spec,
    actions: [
      { ...spec.actions[0], args: { properties: { number: text }, required: ['number'] } },
      { ...spec.actions[1], args: { properties: { state: text }, required: ['state'] } },
      { ...spec.actions[2], args: { properties: { text } } },
    ],
  };
}

const probe: { voice: VoiceApi | null } = { voice: null };
function Screen({ spec }: { spec: ScreenVoiceSpec<FlowAction> }) {
  useScreenVoice(spec as ScreenVoiceSpec);
  const voice = useVoice();
  useEffect(() => {
    probe.voice = voice;
  });
  return <VoiceSheetsHost />;
}
const voice = () => probe.voice!;

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
    await renderWithVoice(<Screen spec={screenSpec(context)} />, {
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

  it('typed Post as a note retries as a note and applies it with origin typed', async () => {
    const context: FlowContext = { key: 'SHIP-906', state: 'New', revision: 4, decisions: [] };
    const model = new FakeCommandModel();
    // Nothing the model can parse: the typed words come back unclear.
    model.respond([]);
    const view = await renderWithVoice(<Screen spec={screenSpec(context)} />, {
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
        opts: { undo: UNDO.typed, origin: 'typed', allOrNothing: true },
        batchId: 'batch-1',
      },
    ]);
  });
});
