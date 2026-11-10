import { fireEvent, screen } from '@testing-library/react-native';
import { aProject } from '../test-support/builders';
import { designInbox } from '../test-support/inboxDesign';
import { inboxApi } from '../test-support/inbox';
import { renderApp } from '../test-support/renderApp';
import { installFakeEngines } from '../test-support/voiceUi';
import { aFeatureItem, aHistory, workItemApi } from '../test-support/workItem';
import { micSlotId, type MicPlacement } from './MicSlot.testIds';
import { micPermissionIds } from './sheets/MicPermissionSheet.testIds';
import { commandComposerId, commandComposerReasonId } from './typed/CommandComposer.testIds';
import { typedCommandSheetId } from './typed/TypedCommandSheet.testIds';
import { workItemHeaderId } from '../workItem/testIds';

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
    micOff();
    await renderApp({ url: '/p/SHIP/inbox', api: inboxApi({}, { inbox: designInbox() }) });
    await screen.findAllByText('Mic off');
    await tapMic('tabBar');
    await fireEvent.press(await screen.findByTestId(micPermissionIds.typeInstead));
    expect(await screen.findByTestId(typedCommandSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(commandComposerReasonId)).toHaveTextContent(
      'Microphone is off · typing instead',
    );
  });
});
