// J-voice-a: a voice change on the Work item, through the real app, runner and Work item voice spec, with the fake
// recognizer and command model. Every step checks what reached the server.
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { undoneToastId, undoToastId } from '../../actions/UndoToastHost.testIds';
import { dockIds } from '../../dock/testIds';
import { readyCoachMarkIds } from '../../setup/ReadyCoachMark.testIds';
import { aConflict, aProject, aWriteResult } from '../../test-support/builders';
import type { FakeRequest } from '../../test-support/fakeApi';
import { FEATURE_STATES, aFeatureStates } from '../../test-support/features';
import { setConnected } from '../../test-support/netInfo';
import { renderApp } from '../../test-support/renderApp';
import { kvValues } from '../../test-support/storage';
import type { ParsedCall } from '../contracts';
import { installFakeEngines } from '../../test-support/voiceUi';
import {
  NOTE_IDS,
  aDecision,
  aFeatureItem,
  aHistory,
  anItemAsks,
  workItemApi,
} from '../../test-support/workItem';
import { primitiveIds } from '../../ui/testIds';
import { workItemHeaderId } from '../../workItem/testIds';
import { undoPartialChangeId, undoPartialSheetId } from '../after/UndoPartialSheet.testIds';
import { undoPreviewConfirmId, undoPreviewSheetId } from '../after/UndoPreviewSheet.testIds';
import {
  voiceChangeId,
  voiceChangeRowId,
  voiceChangeUndoId,
  voiceChangeWaitingId,
} from '../after/VoiceChangeRow.testIds';
import { micSlotId } from '../MicSlot.testIds';
import {
  applyFailedGroupId,
  applyFailedHeadingId,
  applyFailedSheetId,
} from '../sheets/ApplyFailedSheet.testIds';
import { heardQuoteId, heardQuoteLabelId } from '../sheets/HeardQuote.testIds';
import {
  previewApplyId,
  previewFixId,
  previewLineId,
  previewRemoveId,
  previewSheetId,
} from '../sheets/PreviewSheet.testIds';
import { commandComposerFieldId, commandComposerPreviewId } from '../typed/CommandComposer.testIds';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const PROJECT = aProject().id;
const [NEW, ACTIVE] = FEATURE_STATES;
// SHIP-906 in New, so "Set this to Active" is a change.
const ITEM = aFeatureItem({
  projectId: PROJECT,
  state: { id: NEW.id, name: NEW.name, category: 'proposed' },
});
const ITEM_URL = `/p/SHIP/items/${ITEM.key}`;
const PATCH = `PATCH /api/work-items/${ITEM.id}`;
const CONFIRM = `POST /api/work-items/${ITEM.id}/decisions/confirm`;
const COMMENT = `POST /api/work-items/${ITEM.id}/comments`;
const ARCHIVE = `POST /api/comments/${NOTE_IDS[0]}/archive`;
const WAITING = new RegExp(`^${voiceChangeWaitingId('')}`);
const line = (kind: 'addNote' | 'setState' | 'approve') => `workItem:${ITEM.id}:${kind}`;

const NOTE = 'the API returns 500 on retry';
const SAID = `Set this to Active, approve D10, and add a note: ${NOTE}`;
const noteCall = (text: string, note: string): ParsedCall => {
  const start = text.indexOf(note);
  return { action: 'add_note', args: { note }, noteSpan: { start, end: start + note.length } };
};
const SET_ACTIVE: ParsedCall = { action: 'set_state', args: { state: 'Active' } };
const APPROVE_D10: ParsedCall = { action: 'approve_decision', args: { decisions: [10] } };

interface PatchBody {
  expectedRevision: number;
  changes: { state: { value: string } | null };
}

// The Work item server, with SHIP-906 kept as it is: a PATCH at the item's revision applies its state and moves the
// revision on, a stale one is a 409 with the item as it is now, and the by-key read shows the item as it is now (so
// Undo sees what the send wrote).
function server() {
  let current: WorkItemDto = structuredClone(ITEM);
  const api = workItemApi(
    {
      [`GET /api/projects/${PROJECT}/states`]: aFeatureStates(),
      [`GET /api/projects/${PROJECT}/work-items/by-key/${ITEM.key}`]: () => current,
      [PATCH]: (request: FakeRequest) => {
        const body = request.body as PatchBody;
        if (body.expectedRevision !== current.revision)
          return { status: 409, body: aConflict(current) };
        const to = FEATURE_STATES.find((s) => s.id === body.changes.state?.value);
        current = {
          ...current,
          revision: current.revision + 1,
          ...(to === undefined
            ? {}
            : { state: { id: to.id, name: to.name, category: to.category } }),
        };
        return aWriteResult({ id: ITEM.id, key: ITEM.key, revision: current.revision });
      },
    },
    {
      projectId: PROJECT,
      items: [ITEM],
      asks: [anItemAsks(ITEM.id, { decisions: [aDecision({ number: 10, label: 'D10' })] })],
      history: aHistory({ revisions: [], asks: [] }),
    },
  );
  return {
    api,
    current: () => current,
    // Someone else edits SHIP-906 on the web: the revision moves on.
    editElsewhere: () => {
      current = { ...current, revision: current.revision + 1, title: `${current.title} (edited)` };
    },
  };
}

const patchBodies = (api: ReturnType<typeof server>['api']) =>
  api.callsTo(PATCH).map((c) => c.body as PatchBody);

const at = { nativeEvent: { locationX: 20, locationY: 20 } };
// Press the dock's mic, hold it, let go, and let the parse settle.
async function hold() {
  const mic = screen.getAllByTestId(micSlotId('dock'))[0];
  await act(async () => fireEvent(mic, 'responderGrant', at));
  await act(async () => {
    jest.advanceTimersByTime(900);
  });
  await act(async () => fireEvent(mic, 'responderRelease', at));
  await act(async () => {
    jest.advanceTimersByTime(0);
  });
}

async function advance(ms: number) {
  await act(async () => {
    jest.advanceTimersByTime(ms);
  });
}

// Opens SHIP-906 with the fake engines and puts the ready coach mark away.
async function open(api: ReturnType<typeof server>['api']) {
  const engines = installFakeEngines();
  await renderApp({ url: ITEM_URL, api });
  await screen.findByTestId(workItemHeaderId);
  const skip = screen.queryByTestId(readyCoachMarkIds.skip);
  if (skip !== null) await fireEvent.press(skip);
  return engines;
}

// Says the text with the model answering these calls, and waits for the preview.
async function say(
  engines: ReturnType<typeof installFakeEngines>,
  text: string,
  calls: ParsedCall[],
) {
  engines.recognizer.script([], text);
  engines.model.respond(calls);
  await hold();
  return screen.findByTestId(previewSheetId);
}

beforeEach(() => {
  jest.useFakeTimers({ now: NOW });
  for (const k of [...kvValues.keys()])
    if (k.startsWith('shiplean.outbox') || k.startsWith('shiplean.voice.')) kvValues.delete(k);
  setConnected(true);
});
afterEach(() => jest.useRealTimers());

describe('VoiceApplyJourney', () => {
  it('holds on SHIP-906, removes the note line, applies, and sends only the state change and D10', async () => {
    const { api } = server();
    const engines = await open(api);

    await say(engines, SAID, [SET_ACTIVE, APPROVE_D10, noteCall(SAID, NOTE)]);
    expect(screen.getByTestId(previewLineId(line('addNote')))).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(previewRemoveId(line('addNote'))));
    await fireEvent.press(screen.getByTestId(previewApplyId));

    // Voice is sent at once: no hold.
    await waitFor(() => expect(api.callsTo(CONFIRM)).toHaveLength(1));
    expect(patchBodies(api)).toEqual([
      {
        expectedRevision: ITEM.revision,
        changes: expect.objectContaining({ state: { value: ACTIVE.id } }),
      },
    ]);
    expect(api.callsTo(CONFIRM)[0].body).toEqual({ numbers: [10] });
    expect(api.callsTo(COMMENT)).toHaveLength(0);
  });

  it('undoes from the toast inside 8 seconds: the state goes back and the approval is listed as staying', async () => {
    const { api, current } = server();
    const engines = await open(api);
    await say(engines, SAID, [SET_ACTIVE, APPROVE_D10]);
    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(CONFIRM)).toHaveLength(1));

    await advance(7000);
    const toast = await screen.findByTestId(undoToastId);
    await fireEvent.press(within(toast).getByTestId(primitiveIds.toastAction));

    await waitFor(() => expect(patchBodies(api)).toHaveLength(2));
    expect(patchBodies(api)[1]).toEqual({
      expectedRevision: ITEM.revision + 1,
      changes: expect.objectContaining({ state: { value: NEW.id } }),
    });
    expect(current().state.id).toBe(NEW.id);
    const sheet = await screen.findByTestId(undoPartialSheetId);
    // The approval stayed: Change D10 is offered instead.
    expect(within(sheet).getByTestId(undoPartialChangeId(10))).toBeOnTheScreen();
    expect(screen.queryByTestId(undoneToastId)).toBeNull();
    // The approval has no reverse: nothing more was sent.
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
  });

  it('says Undone when every change had a reverse', async () => {
    const { api } = server();
    const engines = await open(api);
    const said = `Set this to Active and add a note: ${NOTE}`;
    await say(engines, said, [SET_ACTIVE, noteCall(said, NOTE)]);
    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(COMMENT)).toHaveLength(1));
    expect(api.callsTo(COMMENT)[0].body).toEqual(expect.objectContaining({ body: NOTE }));

    const toast = await screen.findByTestId(undoToastId);
    await fireEvent.press(within(toast).getByTestId(primitiveIds.toastAction));

    expect(await screen.findByTestId(undoneToastId)).toBeOnTheScreen();
    expect(patchBodies(api).map((b) => b.changes.state?.value)).toEqual([ACTIVE.id, NEW.id]);
    expect(api.callsTo(ARCHIVE)).toHaveLength(1);
    expect(screen.queryByTestId(undoPartialSheetId)).toBeNull();
  });

  it('undoes later from Your voice change after a preview', async () => {
    const { api } = server();
    const engines = await open(api);
    await say(engines, SAID, [SET_ACTIVE, APPROVE_D10]);
    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(CONFIRM)).toHaveLength(1));

    // Past the toast's 8 seconds.
    await advance(9000);
    expect(screen.queryByTestId(undoToastId)).toBeNull();
    const row = await screen.findByTestId(voiceChangeRowId);
    await fireEvent.press(await within(row).findByTestId(new RegExp(`^${voiceChangeUndoId('')}`)));

    // The preview first: nothing is sent until Undo.
    expect(await screen.findByTestId(undoPreviewSheetId)).toBeOnTheScreen();
    expect(patchBodies(api)).toHaveLength(1);
    await fireEvent.press(screen.getByTestId(undoPreviewConfirmId));

    await waitFor(() => expect(patchBodies(api)).toHaveLength(2));
    expect(patchBodies(api)[1]).toEqual({
      expectedRevision: ITEM.revision + 1,
      changes: expect.objectContaining({ state: { value: NEW.id } }),
    });
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
  });

  it('keeps the approval and reverses nothing else when the state change after it fails online, and lists what stayed', async () => {
    const { api } = server();
    const engines = await open(api);
    api.failNext(PATCH, { status: 500, body: { error: 'Server error.', code: 'server' } });
    await say(engines, SAID, [APPROVE_D10, SET_ACTIVE]);
    await fireEvent.press(screen.getByTestId(previewApplyId));

    const sheet = await screen.findByTestId(applyFailedSheetId);
    expect(within(sheet).getByTestId(applyFailedHeadingId)).toHaveTextContent(
      'Some changes stayed applied',
    );
    expect(within(sheet).getByTestId(applyFailedGroupId('stillApplied'))).toBeOnTheScreen();
    expect(within(sheet).queryByText(/Nothing changed/)).toBeNull();
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    expect(api.callsTo(PATCH)).toHaveLength(1);
  });

  it('says nothing was changed when the first change fails', async () => {
    const { api } = server();
    const engines = await open(api);
    api.failNext(CONFIRM, { status: 500, body: { error: 'Server error.', code: 'server' } });
    await say(engines, SAID, [APPROVE_D10, SET_ACTIVE]);
    await fireEvent.press(screen.getByTestId(previewApplyId));

    const sheet = await screen.findByTestId(applyFailedSheetId);
    expect(within(sheet).getByTestId(applyFailedHeadingId)).toHaveTextContent(
      'Nothing was changed',
    );
    expect(within(sheet).queryByTestId(applyFailedGroupId('stillApplied'))).toBeNull();
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    expect(api.callsTo(PATCH)).toHaveLength(0);
  });

  it('says yes to apply while the preview is open', async () => {
    const { api } = server();
    const engines = await open(api);
    await say(engines, SAID, [SET_ACTIVE, APPROVE_D10]);

    engines.recognizer.script([], 'yes');
    await hold();

    await waitFor(() => expect(api.callsTo(CONFIRM)).toHaveLength(1));
    expect(patchBodies(api)).toHaveLength(1);
    // "yes" never went to the model.
    expect(engines.model.parsed).toHaveLength(1);
    expect(screen.queryByTestId(previewSheetId)).toBeNull();
  });

  it('applies offline, shows Waiting to send, and sends each change once when back online', async () => {
    const { api } = server();
    const engines = await open(api);
    await act(async () => setConnected(false));
    await say(engines, SAID, [SET_ACTIVE, APPROVE_D10]);
    await fireEvent.press(screen.getByTestId(previewApplyId));

    const row = await screen.findByTestId(voiceChangeRowId);
    expect(await within(row).findByTestId(WAITING)).toBeOnTheScreen();
    expect(api.callsTo(PATCH)).toHaveLength(0);
    expect(api.callsTo(CONFIRM)).toHaveLength(0);

    await act(async () => setConnected(true));
    await waitFor(() => expect(api.callsTo(CONFIRM)).toHaveLength(1));
    await advance(30_000);
    expect(patchBodies(api)).toEqual([
      {
        expectedRevision: ITEM.revision,
        changes: expect.objectContaining({ state: { value: ACTIVE.id } }),
      },
    ]);
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    expect(screen.queryByTestId(WAITING)).toBeNull();
  });

  it('posts a typed command through the preview with You typed and Edit', async () => {
    const { api } = server();
    const engines = await open(api);
    engines.model.respond([APPROVE_D10]);
    await fireEvent.press(screen.getByTestId(dockIds.open));
    await fireEvent.press(await screen.findByTestId(dockIds.modeCommand));
    await fireEvent.changeText(screen.getByTestId(commandComposerFieldId), 'approve D10');
    await fireEvent.press(screen.getByTestId(commandComposerPreviewId));

    const preview = await screen.findByTestId(previewSheetId);
    expect(within(preview).getByTestId(heardQuoteLabelId(heardQuoteId))).toHaveTextContent(
      'You typed',
    );
    expect(within(preview).getByTestId(previewFixId)).toHaveTextContent('Edit');
    expect(api.callsTo(CONFIRM)).toHaveLength(0);
    await fireEvent.press(within(preview).getByTestId(previewApplyId));

    await waitFor(() => expect(api.callsTo(CONFIRM)).toHaveLength(1));
    expect(api.callsTo(CONFIRM)[0].body).toEqual({ numbers: [10] });
    // A typed batch never shows as Your voice change.
    const rowIds = screen
      .queryAllByTestId(new RegExp(`^${voiceChangeId('')}`))
      .map((e) => e.props.testID as string);
    expect(rowIds).toEqual([voiceChangeRowId]);
  });
});
