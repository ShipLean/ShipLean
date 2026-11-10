import { act, fireEvent, screen } from '@testing-library/react-native';
import { useEffect } from 'react';
import { InMemoryActionRunner } from '../../actions/InMemoryActionRunner';
import { systemNow } from '../../actions/clock';
import type { ActionRunner, AppAction, PreviewLine } from '../../actions/types';
import { UNDO } from '../../actions/undo';
import { UndoToastHost } from '../../actions/UndoToastHost';
import { undoToastId } from '../../actions/UndoToastHost.testIds';
import type { FakeRunner } from '../../test-support/actions';
import { renderScreen } from '../../test-support/renderScreen';
import { aTranscript, FakeCommandModel, FakeRecognizer } from '../../test-support/voice';
import { renderWithVoice } from '../../test-support/voiceUi';
import { primitiveIds } from '../../ui/testIds';
import type { ParsedCall, ScreenVoiceSpec } from '../contracts';
import { useVoiceInternals, type VoiceInternals, type VoiceUiStateOf } from '../internals';
import { useScreenVoice, useVoice, VoiceSheetsHost, type VoiceApi } from '../VoiceProvider';
import { heardQuoteId, heardQuoteLabelId } from './HeardQuote.testIds';
import { PreviewSheet } from './PreviewSheet';
import {
  previewApplyId,
  previewFixId,
  previewLineId,
  previewMarkId,
  previewRemoveId,
  previewSayYesId,
  previewSheetId,
  previewTitleId,
  previewWhyId,
} from './PreviewSheet.testIds';

const NOW = new Date('2030-01-01T09:00:00.000Z');

const action = (kind: string): AppAction => ({
  area: 'workItem',
  kind,
  itemId: 'item-906',
  key: 'SHIP-906',
});
const TEXT: Record<string, string> = {
  setState: 'State: New → Active',
  approve: 'Approve D10',
  addNote: 'Note on SHIP-906',
};

// One change per call, its line worded from TEXT: lines[i] describes actions[i].
const workItemSpec: ScreenVoiceSpec = {
  screen: 'SHIP-906 Voice commands',
  actions: [],
  context: () => ({}),
  hotwords: () => ['SHIP-906'],
  toActions: (calls: ParsedCall[]) => ({
    ok: true,
    actions: calls.map((c) => action(c.action)),
    lines: calls.map((c): PreviewLine => ({
      id: c.action,
      icon: 'flag',
      text: TEXT[c.action],
      removable: true,
    })),
    rejected: [],
    view: null,
  }),
};

const THREE: ParsedCall[] = [
  { action: 'setState', args: {} },
  { action: 'approve', args: {} },
  { action: 'addNote', args: {} },
];

const probe: { voice: VoiceApi | null; internals: VoiceInternals | null } = {
  voice: null,
  internals: null,
};
function Screen() {
  useScreenVoice(workItemSpec);
  const voice = useVoice();
  const internals = useVoiceInternals();
  useEffect(() => {
    probe.voice = voice;
    probe.internals = internals;
  });
  return <VoiceSheetsHost />;
}
const voice = () => probe.voice!;
const internals = () => probe.internals!;

async function mount<R extends ActionRunner = FakeRunner>(runner?: R) {
  const recognizer = new FakeRecognizer();
  const model = new FakeCommandModel();
  model.respond(THREE);
  const view = await renderWithVoice<R>(
    <>
      <Screen />
      <UndoToastHost />
    </>,
    { engines: { recognizer, model }, runner },
  );
  return { recognizer, model, runner: view.runner };
}

async function say(recognizer: FakeRecognizer, words: string) {
  recognizer.script([], words);
  await act(async () => voice().holdStart());
  await act(async () => {
    jest.advanceTimersByTime(900);
  });
  await act(async () => voice().holdEnd());
}

beforeEach(() => jest.useFakeTimers({ now: NOW }));
afterEach(() => jest.useRealTimers());

describe('PreviewSheet', () => {
  it('removing a line and Apply dispatches the rest with UNDO.voice, origin voice, allOrNothing', async () => {
    const { recognizer, runner } = await mount();
    await say(recognizer, 'set this to Active, approve D10, and add a note: fine for v1');
    expect(screen.getByTestId(previewTitleId)).toHaveTextContent('3 changes to apply');
    expect(screen.getByTestId(heardQuoteLabelId(heardQuoteId))).toHaveTextContent('Heard');
    expect(screen.getByTestId(previewSayYesId)).toBeOnTheScreen();

    await fireEvent.press(screen.getByTestId(previewRemoveId('approve')));
    expect(screen.queryByTestId(previewLineId('approve'))).toBeNull();
    expect(screen.getByTestId(previewApplyId)).toHaveTextContent('Apply 2');

    await fireEvent.press(screen.getByTestId(previewApplyId));
    expect(runner.dispatched).toEqual([
      {
        batch: [action('setState'), action('addNote')],
        opts: { undo: UNDO.voice, origin: 'voice', allOrNothing: true },
        batchId: 'batch-1',
      },
    ]);
    expect(runner.dispatched[0].opts.undo).toBe(UNDO.voice);
    expect(screen.queryByTestId(previewSheetId)).toBeNull();
  });

  it('a typed preview dispatches with UNDO.typed and origin typed, offers Edit, and has no "Or say yes"', async () => {
    const { runner } = await mount();
    await act(async () =>
      internals().resolve(aTranscript({ text: 'set this to Active and approve D10' }), {
        source: 'typed',
      }),
    );
    expect(screen.getByTestId(heardQuoteLabelId(heardQuoteId))).toHaveTextContent('You typed');
    expect(screen.getByTestId(previewFixId)).toHaveTextContent('Edit');
    expect(screen.queryByTestId(previewSayYesId)).toBeNull();

    await fireEvent.press(screen.getByTestId(previewApplyId));
    expect(runner.dispatched).toEqual([
      {
        batch: [action('setState'), action('approve'), action('addNote')],
        opts: { undo: UNDO.typed, origin: 'typed', allOrNothing: true },
        batchId: 'batch-1',
      },
    ]);
    // UNDO.typed and UNDO.voice hold the same window today, so check which one was passed.
    expect(runner.dispatched[0].opts.undo).toBe(UNDO.typed);
  });

  it('saying yes while the preview is open applies without calling the model', async () => {
    const { recognizer, model, runner } = await mount();
    await say(recognizer, 'set this to Active, approve D10, and add a note: fine for v1');
    expect(model.parsed).toHaveLength(1);
    await fireEvent.press(screen.getByTestId(previewRemoveId('addNote')));

    await say(recognizer, 'Yes.');
    expect(model.parsed).toHaveLength(1);
    expect(runner.dispatched).toEqual([
      {
        batch: [action('setState'), action('approve')],
        opts: { undo: UNDO.voice, origin: 'voice', allOrNothing: true },
        batchId: 'batch-1',
      },
    ]);
  });

  it('Undo on the toast calls runner.undo with the batch id', async () => {
    const inMemory = new InMemoryActionRunner({ now: systemNow });
    // The toast words each pending action through its area's executor.
    inMemory.register<AppAction>({
      area: 'workItem',
      send: async () => {},
      describe: (a) => ({ id: a.kind, icon: 'flag', text: TEXT[a.kind], removable: true }),
      reversible: () => true,
      undo: async () => {},
    });
    const { recognizer, runner } = await mount(inMemory);
    const undo = jest.spyOn(runner, 'undo');
    await say(recognizer, 'set this to Active, approve D10, and add a note: fine for v1');
    await fireEvent.press(screen.getByTestId(previewApplyId));
    expect(screen.getByTestId(undoToastId)).toHaveTextContent(
      /State: New → Active · Approve D10 · Note on SHIP-906/,
    );

    await fireEvent.press(screen.getByTestId(primitiveIds.toastAction));
    expect(undo).toHaveBeenCalledWith('batch-1');
  });

  it('strikes out a part that cannot be done with its reason, and shows each mark under its line', async () => {
    const state: VoiceUiStateOf<'preview'> = {
      kind: 'preview',
      heard: 'set this to Active and split it',
      source: 'voice',
      lines: [
        { id: 'setState', icon: 'flag', text: 'State: New → Active', removable: true },
        {
          id: 'rejected:0',
          icon: 'block',
          text: 'Split the item',
          warning: 'Do this on the web.',
          removable: false,
        },
      ],
      removed: [],
      marks: [{ lineId: 'setState', note: 'Changed by Fix' }],
      fixing: false,
    };
    const applied: string[][] = [];
    await renderScreen(
      <PreviewSheet
        state={state}
        onApply={(ids) => applied.push(ids)}
        onFix={() => {}}
        onCancel={() => {}}
      />,
    );
    expect(screen.getByTestId(previewLineId('rejected:0'))).toBeDisabled();
    expect(screen.getByTestId(previewWhyId('rejected:0'))).toHaveTextContent('Do this on the web.');
    expect(screen.queryByTestId(previewRemoveId('rejected:0'))).toBeNull();
    expect(screen.getByTestId(previewMarkId('setState'))).toHaveTextContent('Changed by Fix');
    expect(screen.getByTestId(previewTitleId)).toHaveTextContent('1 change to apply');

    await fireEvent.press(screen.getByTestId(previewApplyId));
    expect(applied).toEqual([['setState']]);
  });
});
