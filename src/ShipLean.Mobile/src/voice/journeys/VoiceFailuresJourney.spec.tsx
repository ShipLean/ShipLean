// J-voice-c: every voice failure in the wired app says what happened and offers the next step, and nothing is sent
// until a correction is applied.
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import type { SearchItemDto, SearchViewDto } from '@shiplean/core/api/search';
import { inboxRowId } from '../../inbox/testIds';
import { screenIds } from '../../screens/testIds';
import { readyCoachMarkIds } from '../../setup/ReadyCoachMark.testIds';
import { aProject } from '../../test-support/builders';
import { backlogApi, designFeatures } from '../../test-support/backlog';
import { controlledApi } from '../../test-support/controlledApi';
import { inboxApi } from '../../test-support/inbox';
import { designInbox } from '../../test-support/inboxDesign';
import { renderApp } from '../../test-support/renderApp';
import { signedInApi } from '../../test-support/signedInApi';
import { kvValues } from '../../test-support/storage';
import { installFakeEngines } from '../../test-support/voiceUi';
import { primitiveIds } from '../../ui/testIds';
import { correctionClosestId, correctionFailureYesId } from '../failures/CorrectionFailure.testIds';
import { micCheckId } from '../failures/MicCheck.testIds';
import { resetSilentHolds } from '../failures/silentHolds';
import { micSlotId, type MicPlacement } from '../MicSlot.testIds';
import { previewApplyId, previewSheetId } from '../sheets/PreviewSheet.testIds';

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
const backlog = (extra: Record<string, unknown> = {}) =>
  controlledApi({
    ...signedInApi(),
    ...backlogApi(extra, { projectId: PROJECT, rows: { features: FEATURES } }),
    [`GET /api/projects/${PROJECT}/members`]: [],
  });
const BULK = `POST /api/projects/${PROJECT}/backlog/bulk`;
const rowOf = (key: string) => FEATURES.find((r) => r.key === key)!;

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
    const preview = await screen.findByTestId(previewSheetId);
    expect(within(preview).getByText(/SHIP-906/)).toBeOnTheScreen();
    expect(api.callsTo(BULK)).toHaveLength(0);

    await fireEvent.press(screen.getByTestId(previewApplyId));
    await waitFor(() => expect(api.callsTo(BULK)).toHaveLength(1));
    const body = api.callsTo(BULK)[0].body as { items: { id: string }[] };
    expect(body.items.map((i) => i.id)).toContain(rowOf('SHIP-906').id);
  });
});
