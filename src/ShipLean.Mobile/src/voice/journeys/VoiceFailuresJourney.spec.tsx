// J-voice-c: every voice failure in the wired app says what happened and offers the next step, and nothing is sent
// until a correction is applied.
import type { SearchItemDto, SearchViewDto } from '@shiplean/core/api/search';
import { platform } from '@shiplean/core/platform';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { openBrowserAsync } from 'expo-web-browser';
import { dockIds } from '../../dock/testIds';
import { inboxRowId } from '../../inbox/testIds';
import { screenIds } from '../../screens/testIds';
import { readyCoachMarkIds } from '../../setup/ReadyCoachMark.testIds';
import { aProject } from '../../test-support/builders';
import { backlogApi, designFeatures } from '../../test-support/backlog';
import { controlledApi } from '../../test-support/controlledApi';
import type { ApiTable } from '../../test-support/fakeApi';
import { inboxApi } from '../../test-support/inbox';
import { designInbox } from '../../test-support/inboxDesign';
import { renderApp } from '../../test-support/renderApp';
import { signedInApi } from '../../test-support/signedInApi';
import { aFeatureItem, aHistory, workItemApi } from '../../test-support/workItem';
import { workItemHeaderId } from '../../workItem/testIds';
import { kvValues } from '../../test-support/storage';
import { installFakeEngines } from '../../test-support/voiceUi';
import { primitiveIds } from '../../ui/testIds';
import {
  correctionClosestId,
  correctionFailureDetailId,
  correctionFailureNothingChangedId,
  correctionFailureOpenWebId,
  correctionFailureYesId,
} from '../failures/CorrectionFailure.testIds';
import { micCheckId } from '../failures/MicCheck.testIds';
import { resetSilentHolds } from '../failures/silentHolds';
import { micSlotId, type MicPlacement } from '../MicSlot.testIds';
import { micPermissionIds, micPermissionSheetId } from '../sheets/MicPermissionSheet.testIds';
import {
  previewApplyId,
  previewLineId,
  previewSheetId,
  previewWhyId,
} from '../sheets/PreviewSheet.testIds';
import {
  commandComposerFieldId,
  commandComposerId,
  commandComposerPreviewId,
} from '../typed/CommandComposer.testIds';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const PROJECT = aProject().id;
const at = { nativeEvent: { locationX: 20, locationY: 20 } };

// Press the mic, keep it down for heldMs, let go, and let the parse settle.
async function hold(placement: MicPlacement, heldMs = 900) {
  const mic = screen.getAllByTestId(micSlotId(placement))[0];
  await act(async () => fireEvent(mic, 'responderGrant', at));
  await act(async () => {
    jest.advanceTimersByTime(heldMs);
  });
  await act(async () => fireEvent(mic, 'responderRelease', at));
  await act(async () => {
    jest.advanceTimersByTime(0);
  });
}

// Puts the "Voice is ready" coach mark away, so no guided first command takes over a result.
async function skipCoachMark() {
  await fireEvent.press(await screen.findByTestId(readyCoachMarkIds.skip));
}

// The Features level of the Backlog, built once so its ids are the ones the routes answer for.
const FEATURES = designFeatures();
const backlog = (extra: ApiTable = {}) =>
  controlledApi({
    ...signedInApi(),
    ...backlogApi(extra, { projectId: PROJECT, rows: { features: FEATURES } }),
    [`GET /api/projects/${PROJECT}/members`]: [],
  });
const BULK = `POST /api/projects/${PROJECT}/backlog/bulk`;
const rowOf = (key: string) => FEATURES.find((r) => r.key === key)!;

// Search's answer for words that match nothing: two SHIP items around one from another project.
const searchItem = (key: string, title: string, projectKey: string): SearchItemDto => ({
  key,
  title,
  type: 'feature',
  state: 'Active',
  stateCategory: 'inProgress',
  projectKey,
  parentPath: [],
  matchedIn: 'title',
  snippet: '',
  url: `/p/${projectKey}/items/${key}`,
  attachment: null,
});
const SEARCH: SearchViewDto = {
  query: 'the dictation work',
  semantic: false,
  projects: [],
  sprints: [],
  items: [
    searchItem('SHIP-906', rowOf('SHIP-906').title, 'SHIP'),
    searchItem('OPS-12', 'Dictation for the support desk', 'OPS'),
    searchItem('SHIP-891', rowOf('SHIP-891').title, 'SHIP'),
  ],
  more: false,
};

// The Work item screen on SHIP-906, a Feature.
const ITEM = aFeatureItem({ projectId: PROJECT });
const ITEM_URL = `/p/SHIP/items/${ITEM.key}`;
const ITEM_PATCH = `PATCH /api/work-items/${ITEM.id}`;
const workItem = () =>
  workItemApi(
    { [`GET /api/projects/${PROJECT}/inbox`]: designInbox() },
    { projectId: PROJECT, items: [ITEM], history: aHistory({ revisions: [], asks: [] }) },
  );
const changesOf = (call: { body: unknown }) =>
  (call.body as { changes: Record<string, unknown> }).changes;

beforeEach(() => {
  jest.useFakeTimers({ now: NOW });
  for (const k of [...kvValues.keys()])
    if (k.startsWith('shiplean.outbox') || k.startsWith('shiplean.voice.')) kvValues.delete(k);
  resetSilentHolds();
});
afterEach(() => jest.useRealTimers());

describe('VoiceFailuresJourney', () => {
  it('shows the hold hint for a tap and nothing was changed for silence, then the mic check on the third silent hold', async () => {
    const { model } = installFakeEngines();
    await renderApp({ url: '/p/SHIP/inbox', api: inboxApi({}, { inbox: designInbox() }) });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    await skipCoachMark();

    // A tap: the hold hint, no sheet, and the model is never asked.
    await hold('tabBar', 200);
    expect(await screen.findByTestId(primitiveIds.toast)).toHaveTextContent(
      'Hold the mic while you speak',
    );
    expect(screen.queryByTestId(micCheckId)).toBeNull();

    // Two silent holds: nothing was changed, each time, and still no mic check.
    for (let i = 0; i < 2; i += 1) {
      await hold('tabBar');
      expect(await screen.findByTestId(primitiveIds.toast)).toHaveTextContent(
        "Didn't hear anything · nothing was changed",
      );
      expect(screen.queryByTestId(micCheckId)).toBeNull();
    }

    // The third silent hold in a row opens the mic check instead of another toast.
    await hold('tabBar');
    expect(await screen.findByTestId(micCheckId)).toBeOnTheScreen();
    expect(model.parsed).toHaveLength(0);
  });

  it('offers SHIP-906 for SHIP-960 on the Backlog and goes on to the rank preview', async () => {
    const { model, recognizer } = installFakeEngines();
    const api = backlog();
    await renderApp({ url: '/p/SHIP/backlog', api });
    await screen.findByTestId(screenIds.backlog);
    await skipCoachMark();

    recognizer.script([], 'move SHIP-960 to the top');
    model.respond([{ action: 'move_to', args: { item: 'SHIP-960', position: 'top' } }]);
    await hold('tabBar');

    const yes = await screen.findByTestId(correctionFailureYesId);
    expect(yes).toHaveTextContent(/SHIP-906/);
    expect(api.callsTo(BULK)).toHaveLength(0);

    await fireEvent.press(yes);
    // The rank preview for the suggested key: SHIP-906 from 4th to the top. Still nothing sent.
    const preview = await screen.findByTestId(previewSheetId);
    expect(
      within(preview).getByTestId(previewLineId(`move:${rowOf('SHIP-906').id}`)),
    ).toHaveTextContent(/^SHIP-906 4 → 1/);
    expect(api.callsTo(BULK)).toHaveLength(0);

    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(BULK)).toHaveLength(1));
    const body = api.callsTo(BULK)[0].body as { items: { id: string }[] };
    expect(body.items.map((i) => i.id)).toContain(rowOf('SHIP-906').id);
  });

  it("offers search's two closest for an item that matches nothing, and a tap runs the command on it", async () => {
    const { model, recognizer } = installFakeEngines();
    const api = backlog({ 'GET /api/search': SEARCH });
    await renderApp({ url: '/p/SHIP/backlog', api });
    await screen.findByTestId(screenIds.backlog);
    await skipCoachMark();

    recognizer.script([], 'move the dictation work to the top');
    model.respond([{ action: 'move_to', args: { item: 'the dictation work', position: 'top' } }]);
    await hold('tabBar');

    // Search's two closest in this project, in search's order; the other project's item is left out.
    expect(await screen.findByTestId(correctionClosestId(0))).toHaveTextContent(
      `SHIP-906 ${rowOf('SHIP-906').title}`,
    );
    expect(screen.getByTestId(correctionClosestId(1))).toHaveTextContent(
      `SHIP-891 ${rowOf('SHIP-891').title}`,
    );
    expect(screen.queryByTestId(correctionClosestId(2))).toBeNull();
    expect(screen.getByTestId(correctionFailureNothingChangedId)).toBeOnTheScreen();
    const asked = api.callsTo('GET /api/search');
    expect(new URL(asked[asked.length - 1].url).searchParams.get('q')).toBe('the dictation work');
    expect(api.callsTo(BULK)).toHaveLength(0);

    // A tap on the second runs the same command on SHIP-891: its rank preview, and only Apply sends.
    await fireEvent.press(screen.getByTestId(correctionClosestId(1)));
    const preview = await screen.findByTestId(previewSheetId);
    expect(
      within(preview).getByTestId(previewLineId(`move:${rowOf('SHIP-891').id}`)),
    ).toHaveTextContent(/^SHIP-891 5 → 1/);
    expect(model.parsed).toHaveLength(1);
    expect(api.callsTo(BULK)).toHaveLength(0);

    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(BULK)).toHaveLength(1));
    const body = api.callsTo(BULK)[0].body as { items: { id: string }[] };
    expect(body.items.map((i) => i.id)).toContain(rowOf('SHIP-891').id);
  });

  it('says where a web-only action is done', async () => {
    const { model, recognizer } = installFakeEngines();
    const api = workItem();
    await renderApp({ url: ITEM_URL, api });
    await screen.findByTestId(workItemHeaderId);
    jest.mocked(openBrowserAsync).mockClear();

    recognizer.script([], 'edit the title');
    model.respond([{ action: 'edit_fields', args: {} }]);
    await hold('dock');

    expect(await screen.findByTestId(correctionFailureDetailId)).toHaveTextContent(
      'Title, description, criteria, points, sprint or links: on the web item page',
    );
    expect(screen.getByTestId(correctionFailureNothingChangedId)).toBeOnTheScreen();
    expect(screen.queryByTestId(previewSheetId)).toBeNull();

    // The next step: this item on the web.
    await fireEvent.press(screen.getByTestId(correctionFailureOpenWebId));
    expect(openBrowserAsync).toHaveBeenCalledWith(
      `${platform().apiOrigin()}/p/SHIP/items/${ITEM.key}`,
    );
    expect(api.callsTo(ITEM_PATCH)).toHaveLength(0);
  });

  it('strikes out the web-only part and applies the rest', async () => {
    const { model, recognizer } = installFakeEngines();
    const api = workItem();
    await renderApp({ url: ITEM_URL, api });
    await screen.findByTestId(workItemHeaderId);

    recognizer.script([], 'mark this blocked and edit the title');
    model.respond([
      { action: 'set_health', args: { health: 'blocked' } },
      { action: 'edit_fields', args: {} },
    ]);
    await hold('dock');

    // The web-only part is struck out with where it's done; the health change stays to apply.
    const preview = await screen.findByTestId(previewSheetId);
    expect(within(preview).getByTestId(previewLineId('rejected:0'))).toBeOnTheScreen();
    expect(within(preview).getByTestId(previewWhyId('rejected:0'))).toHaveTextContent(
      'Do this on Title, description, criteria, points, sprint or links: on the web item page.',
    );
    expect(api.callsTo(ITEM_PATCH)).toHaveLength(0);

    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(ITEM_PATCH)).toHaveLength(1));
    const changes = changesOf(api.callsTo(ITEM_PATCH)[0]);
    expect(changes).toMatchObject({ health: { value: 'blocked' } });
    // Nothing else changes: every other field is null or an empty list.
    const changed = Object.entries(changes).filter(
      ([, v]) => v !== null && !(Array.isArray(v) && v.length === 0),
    );
    expect(changed.map(([k]) => k)).toEqual(['health']);
  });

  it('opens the permission sheet from a crossed-out mic and Type instead opens the composer on Command', async () => {
    const { mic, model } = installFakeEngines();
    mic.set('deniedFinal');
    const api = workItem();
    await renderApp({ url: ITEM_URL, api });
    await screen.findByTestId(workItemHeaderId);

    // A crossed-out mic opens the permission sheet: the way back is the phone's Settings.
    await hold('dock');
    expect(await screen.findByTestId(micPermissionSheetId)).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(micPermissionIds.openSettings));
    expect(mic.settingsOpened).toBe(1);
    expect(model.parsed).toHaveLength(0);

    // The other way on: Type instead opens the dock's composer on Command, and a typed command previews and applies.
    await hold('dock');
    await fireEvent.press(await screen.findByTestId(micPermissionIds.typeInstead));
    expect(await screen.findByTestId(commandComposerId)).toBeOnTheScreen();
    expect(screen.getByTestId(dockIds.modeCommand)).toBeSelected();

    model.respond([{ action: 'set_health', args: { health: 'blocked' } }]);
    await fireEvent.changeText(screen.getByTestId(commandComposerFieldId), 'mark this blocked');
    await fireEvent.press(screen.getByTestId(commandComposerPreviewId));
    expect(await screen.findByTestId(previewSheetId)).toBeOnTheScreen();
    expect(api.callsTo(ITEM_PATCH)).toHaveLength(0);
    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(ITEM_PATCH)).toHaveLength(1));
    expect(changesOf(api.callsTo(ITEM_PATCH)[0])).toMatchObject({ health: { value: 'blocked' } });
  });
});
