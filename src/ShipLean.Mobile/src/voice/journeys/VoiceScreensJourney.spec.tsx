import { readFileSync } from 'node:fs';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { inboxRowId } from '../../inbox/testIds';
import { retroTileId } from '../../retro/testIds';
import { screenIds } from '../../screens/testIds';
import { firstResultSheetIds } from '../../setup/FirstResultSheet.testIds';
import { fromSetupTipIds } from '../../setup/FromSetupTip.testIds';
import { readyCoachMarkIds } from '../../setup/ReadyCoachMark.testIds';
import { backlogApi, designFeatures } from '../../test-support/backlog';
import { aProject } from '../../test-support/builders';
import { controlledApi } from '../../test-support/controlledApi';
import type { FakeApi } from '../../test-support/fakeApi';
import { FEATURE_STATES, designOverview, featuresApi } from '../../test-support/features';
import { anAnswered, inboxApi } from '../../test-support/inbox';
import { designInbox } from '../../test-support/inboxDesign';
import { renderApp } from '../../test-support/renderApp';
import { designRetro, retroApi } from '../../test-support/retro';
import { signedInApi } from '../../test-support/signedInApi';
import { kvValues } from '../../test-support/storage';
import { installFakeEngines } from '../../test-support/voiceUi';
import { aFeatureItem, aHistory, workItemApi } from '../../test-support/workItem';
import { workItemHeaderId } from '../../workItem/testIds';
import type { ParsedCall, TestSetRow } from '../contracts';
import { micSlotId, type MicPlacement } from '../MicSlot.testIds';
import { previewApplyId, previewSheetId } from '../sheets/PreviewSheet.testIds';

const PROJECT = aProject().id;

// Each fixture is built once: its ids are random, and callsTo needs the real ones.
const INBOX = designInbox(PROJECT);
const FEATURES = designFeatures();
const OVERVIEW = designOverview();
const ITEM = aFeatureItem({ projectId: PROJECT });
const RETRO = designRetro();

const inboxId = (key: string) => INBOX.items.find((i) => i.key === key)!.id;
const backlogId = (key: string) => FEATURES.find((r) => r.key === key)!.id;
const featureId = (key: string) =>
  OVERVIEW.groups.flatMap((g) => g.rows).find((r) => r.key === key)!.id;

// A row of a screen's command test set, as tools/voice-eval reads it.
function testSetRow(file: string, index: number): TestSetRow {
  const rows = readFileSync(new URL(`../testset/${file}.jsonl`, import.meta.url), 'utf8')
    .split('\n')
    .filter((l) => l !== '')
    .map((l) => JSON.parse(l) as TestSetRow);
  return rows[index];
}

// What the model returns for the row: its calls, with a note call pointing at the note's words in the transcript.
function callsFor(row: TestSetRow): ParsedCall[] {
  const { note } = row;
  if (note === null) return row.calls;
  const start = row.utterance.indexOf(note);
  return row.calls.map((c) => ({ ...c, noteSpan: { start, end: start + note.length } }));
}

const backlog = () =>
  controlledApi({
    ...signedInApi(),
    ...backlogApi({}, { projectId: PROJECT, rows: { features: FEATURES } }),
    [`GET /api/projects/${PROJECT}/members`]: [],
  });

interface ScreenRun {
  name: string;
  url: string;
  api: () => FakeApi;
  loaded: string;
  mic: MicPlacement;
  row: TestSetRow;
  route: string;
  body: (row: TestSetRow) => unknown;
}

// Each screen: where it opens, what shows once its spec is registered, the test-set row it runs and the request
// that screen's executor sends for it.
const SCREENS: ScreenRun[] = [
  {
    name: 'Inbox',
    url: '/p/SHIP/inbox',
    api: () => inboxApi({}, { inbox: INBOX }),
    loaded: inboxRowId('SHIP-743'),
    mic: 'tabBar',
    row: testSetRow('inbox', 0),
    route: `POST /api/work-items/${inboxId('SHIP-906')}/decisions/confirm`,
    body: () => ({ numbers: [10] }),
  },
  {
    name: 'Work item',
    url: `/p/SHIP/items/${ITEM.key}`,
    api: () =>
      workItemApi(
        { [`GET /api/projects/${PROJECT}/inbox`]: INBOX },
        { projectId: PROJECT, items: [ITEM], history: aHistory({ revisions: [], asks: [] }) },
      ),
    loaded: workItemHeaderId,
    mic: 'dock',
    row: testSetRow('workItem', 0),
    route: `POST /api/work-items/${ITEM.id}/comments`,
    body: (row) => expect.objectContaining({ body: row.note }),
  },
  {
    name: 'Backlog',
    url: '/p/SHIP/backlog',
    api: backlog,
    loaded: screenIds.backlog,
    mic: 'tabBar',
    row: testSetRow('backlog', 0),
    // A move is a position-only bulk placement (backlog/move/send.ts), not the per-item reorder route.
    route: `POST /api/projects/${PROJECT}/backlog/bulk`,
    body: () => ({
      items: [
        expect.objectContaining({
          id: backlogId('SHIP-906'),
          changes: null,
          placement: { slot: 'before', beforeItemId: backlogId('SHIP-907') },
        }),
      ],
    }),
  },
  {
    name: 'Features',
    url: '/p/SHIP/features',
    api: () => featuresApi({}, { projectId: PROJECT, overview: OVERVIEW }),
    loaded: screenIds.features,
    mic: 'tabBar',
    // Row 7, "Move SHIP-892 to Active": the first rows are read-only answers that send nothing.
    row: testSetRow('features', 7),
    route: `PATCH /api/work-items/${featureId('SHIP-892')}`,
    body: () =>
      expect.objectContaining({
        changes: expect.objectContaining({
          state: expect.objectContaining({
            value: FEATURE_STATES.find((s) => s.name === 'Active')!.id,
          }),
        }),
      }),
  },
  {
    name: 'Retro',
    url: '/p/SHIP/retro',
    api: () => retroApi(signedInApi(), { projectId: PROJECT, retro: RETRO }),
    // The sprint's columns: until the retro has loaded, the spec has no sprint to put the point on.
    loaded: retroTileId('wentBadly'),
    mic: 'tabBar',
    row: testSetRow('retro', 0),
    route: `POST /api/projects/${PROJECT}/retros/points`,
    body: (row) =>
      expect.objectContaining({ text: row.note, category: 'wentBadly', sprintId: RETRO.sprint.id }),
  },
];

const at = { nativeEvent: { locationX: 20, locationY: 20 } };
// Press the mic, hold it, let go, and let the parse settle.
async function hold(placement: MicPlacement) {
  const mic = screen.getAllByTestId(micSlotId(placement))[0];
  await act(async () => fireEvent(mic, 'responderGrant', at));
  await act(async () => {
    jest.advanceTimersByTime(900);
  });
  await act(async () => fireEvent(mic, 'responderRelease', at));
  await act(async () => {
    jest.advanceTimersByTime(0);
  });
}

beforeEach(() => {
  jest.useFakeTimers();
  for (const k of [...kvValues.keys()])
    if (k.startsWith('shiplean.outbox') || k.startsWith('shiplean.voice.')) kvValues.delete(k);
});
afterEach(() => jest.useRealTimers());

describe('voice on each screen of the wired app', () => {
  it('runs one test-set command on each of Inbox, Work item, Backlog, Features and Retro', async () => {
    for (const s of SCREENS) {
      const { model, recognizer } = installFakeEngines();
      const app = await renderApp({ url: s.url, api: s.api() });
      await screen.findByTestId(s.loaded);
      // The ready coach mark shows on the first screen; Skip ends the guide for good, so no first-result sheet
      // takes over an answer on this or any later screen.
      if (s === SCREENS[0]) await fireEvent.press(await screen.findByTestId(readyCoachMarkIds.skip));

      recognizer.script([], s.row.utterance);
      model.respond(callsFor(s.row));
      await hold(s.mic);

      const preview = await screen.findByTestId(previewSheetId);
      expect([s.name, model.parsed.map((p) => p.screen)]).toEqual([s.name, [s.row.screen]]);
      expect([s.name, app.api.callsTo(s.route)]).toEqual([s.name, []]);

      // A voice batch is sent at once on Apply (no 5 s hold: that is for taps).
      await fireEvent.press(within(preview).getByTestId(previewApplyId));
      await waitFor(() =>
        expect([s.name, app.api.callsTo(s.route).map((c) => c.body)]).toEqual([
          s.name,
          [s.body(s.row)],
        ]),
      );
      await app.unmount();
    }
  });

  it('opens the Inbox with the from-setup tip after the guided first command', async () => {
    const { model, recognizer } = installFakeEngines();
    const api = controlledApi({
      ...signedInApi(),
      ...backlogApi({}, { projectId: PROJECT, rows: { features: FEATURES } }),
      [`GET /api/projects/${PROJECT}/members`]: [],
      [`GET /api/projects/${PROJECT}/inbox`]: INBOX,
      [`GET /api/projects/${PROJECT}/inbox/answered`]: anAnswered(),
    });
    await renderApp({ url: '/p/SHIP/backlog', api });
    await screen.findByTestId(screenIds.backlog);
    // Voice is ready, so the "Try it" coach mark shows at the mic.
    expect(await screen.findByTestId(readyCoachMarkIds.card)).toBeOnTheScreen();

    recognizer.script([], 'what needs me');
    model.respond([{ action: 'ask', args: { topic: 'whatNeedsMe' } }]);
    await hold('tabBar');

    // The first answer opens the first-result sheet, whose Open Inbox lands on the Inbox with its tip.
    await fireEvent.press(await screen.findByTestId(firstResultSheetIds.openInbox));
    const tip = await screen.findByTestId(fromSetupTipIds.card);
    expect(within(screen.getByTestId(screenIds.inbox)).getByTestId(fromSetupTipIds.card)).toBe(tip);
    expect(screen.getByTestId(inboxRowId('SHIP-906'))).toBeOnTheScreen();
    expect(screen.queryByTestId(firstResultSheetIds.root)).toBeNull();
  });
});
