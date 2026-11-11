import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { aProject } from '../test-support/builders';
import { designInbox } from '../test-support/inboxDesign';
import { inboxRowId } from '../inbox/testIds';
import { inboxApi } from '../test-support/inbox';
import { renderApp } from '../test-support/renderApp';
import { installFakeEngines } from '../test-support/voiceUi';
import { aFeatureItem, aHistory, workItemApi } from '../test-support/workItem';
import { micSlotId, type MicPlacement } from './MicSlot.testIds';
import { micPermissionIds } from './sheets/MicPermissionSheet.testIds';
import { router } from 'expo-router';
import { screenIds } from '../screens/testIds';
import { requestTypeInstead } from './micTuning/returnIntent';
import { heardQuoteId, heardQuoteLabelId } from './sheets/HeardQuote.testIds';
import { previewSheetId } from './sheets/PreviewSheet.testIds';
import {
  commandComposerFieldId,
  commandComposerId,
  commandComposerPreviewId,
  commandComposerReasonId,
} from './typed/CommandComposer.testIds';
import { typedCommandSheetId } from './typed/TypedCommandSheet.testIds';
import { workItemHeaderId } from '../workItem/testIds';

// The setup's Sentry mock has no addBreadcrumb, which the Mic tuning page calls when it opens.
jest.mock('@sentry/react-native', () => ({
  __esModule: true,
  init: jest.fn(),
  addBreadcrumb: jest.fn(),
}));

const PROJECT = aProject().id;
const ITEM = aFeatureItem({ projectId: PROJECT });
const at = { nativeEvent: { locationX: 20, locationY: 20 } };

async function tapMic(placement: MicPlacement) {
  await fireEvent(screen.getByTestId(micSlotId(placement)), 'responderGrant', at);
  await fireEvent(screen.getByTestId(micSlotId(placement)), 'responderRelease', at);
}

function micOff() {
  const engines = installFakeEngines();
  engines.mic.set('deniedFinal');
  return engines;
}

describe('Type instead from the mic sheet', () => {
  it('opens the Work item dock composer in Command mode', async () => {
    micOff();
    await renderApp({
      url: `/p/SHIP/items/${ITEM.key}`,
      api: workItemApi(
        { [`GET /api/projects/${PROJECT}/inbox`]: designInbox() },
        { projectId: PROJECT, items: [ITEM], history: aHistory({ revisions: [], asks: [] }) },
      ),
    });
    await screen.findByTestId(workItemHeaderId);
    expect(await screen.findByText('Mic off')).toBeOnTheScreen();
    await tapMic('dock');
    await fireEvent.press(await screen.findByTestId(micPermissionIds.typeInstead));
    expect(await screen.findByTestId(commandComposerId)).toBeOnTheScreen();
    expect(screen.getByTestId(commandComposerReasonId)).toHaveTextContent(
      'Microphone is off · typing instead',
    );
    expect(screen.queryByTestId(typedCommandSheetId)).not.toBeOnTheScreen();
  });

  it('opens a Command sheet on a tab root, which has no dock', async () => {
    const { model } = micOff();
    model.respond([{ action: 'approve', args: { key: 'SHIP-906', decisions: [10] } }]);
    await renderApp({ url: '/p/SHIP/inbox', api: inboxApi({}, { inbox: designInbox() }) });
    await screen.findAllByText('Mic off');
    await screen.findByTestId(inboxRowId('SHIP-906'));
    await tapMic('tabBar');
    await fireEvent.press(await screen.findByTestId(micPermissionIds.typeInstead));
    expect(await screen.findByTestId(typedCommandSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(commandComposerReasonId)).toHaveTextContent(
      'Microphone is off · typing instead',
    );
    expect(screen.getByTestId(`${typedCommandSheetId}-keyboard`)).toBeOnTheScreen();

    await fireEvent.changeText(
      screen.getByTestId(commandComposerFieldId),
      'set this to Active and approve D10',
    );
    await fireEvent.press(screen.getByTestId(commandComposerPreviewId));
    expect(await screen.findByTestId(previewSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(heardQuoteLabelId(heardQuoteId))).toHaveTextContent('You typed');
    expect(screen.queryByTestId(typedCommandSheetId)).not.toBeOnTheScreen();
  });

  it('opens the Command sheet when the Mic tuning page asks for Type instead, after three silent holds', async () => {
    installFakeEngines();
    jest.useFakeTimers();
    try {
      await renderApp({ url: '/p/SHIP/inbox', api: inboxApi({}, { inbox: designInbox() }) });
      // The first hold only shows the first-use tip; the next three are silent holds in a row, and the third opens the page.
      for (let hold = 0; hold < 4; hold += 1) {
        await fireEvent(screen.getByTestId(micSlotId('tabBar')), 'responderGrant', at);
        await act(async () => {
          jest.advanceTimersByTime(900);
        });
        await fireEvent(screen.getByTestId(micSlotId('tabBar')), 'responderRelease', at);
        await act(async () => {
          jest.advanceTimersByTime(3000);
        });
      }
      expect(await screen.findByTestId(screenIds.micTuning)).toBeOnTheScreen();
      // The page's Type instead button calls this same pair (requestTypeInstead, then back).
      await act(async () => {
        requestTypeInstead();
        router.back();
      });
      expect(await screen.findByTestId(typedCommandSheetId)).toBeOnTheScreen();
      expect(screen.getByTestId(commandComposerId)).toBeOnTheScreen();
      await waitFor(() => expect(screen.queryByTestId(screenIds.micTuning)).toBeNull());
    } finally {
      jest.useRealTimers();
    }
  });
});
