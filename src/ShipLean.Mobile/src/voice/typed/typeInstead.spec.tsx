import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { useEffect } from 'react';
import { session } from '../../session/session';
import { TEST_ORIGIN, TEST_SECRET, aMe, aProject } from '../../test-support/builders';
import { fakeApi } from '../../test-support/fakeApi';
import { signedInApi } from '../../test-support/signedInApi';
import { renderWithVoice } from '../../test-support/voiceUi';
import { Dock } from '../../ui/Dock';
import { useVoice, type VoiceApi } from '../VoiceProvider';
import {
  commandComposerFieldId,
  commandComposerId,
  commandComposerReasonId,
} from './CommandComposer.testIds';
import { typedCommandSheetId } from './TypedCommandSheet.testIds';
import { TypedCommandSheet } from './TypedCommandSheet';

const probe: { voice: VoiceApi | null } = { voice: null };
function VoiceProbe() {
  const voice = useVoice();
  useEffect(() => {
    probe.voice = voice;
  });
  return null;
}

beforeEach(() => {
  fakeApi({
    ...signedInApi(),
    [`GET /api/projects/${aProject().id}/members`]: [],
  });
  session.abandon();
  session.beginSignIn(TEST_ORIGIN);
  session.setPendingSecret(TEST_SECRET);
  session.commit(aMe().email);
});

describe('typeInstead on the real provider', () => {
  it('opens the focused dock in Command mode with the mic-off line', async () => {
    await renderWithVoice(
      <>
        <VoiceProbe />
        <Dock itemId="i1" itemKey="SHIP-1" projectId={aProject().id} placeholder="Type a note" />
        <TypedCommandSheet />
      </>,
      { engines: {} },
    );
    await act(async () => probe.voice!.typeInstead());
    expect(await screen.findByTestId(commandComposerId)).toBeOnTheScreen();
    expect(screen.getByTestId(commandComposerReasonId)).toHaveTextContent(
      'Microphone is off · typing instead',
    );
    expect(screen.getByTestId(commandComposerFieldId)).toHaveProp('autoFocus', true);
    // The dock took it: no second composer in a sheet.
    expect(screen.queryByTestId(typedCommandSheetId)).not.toBeOnTheScreen();
  });

  it('with no dock on the screen, shows the Command composer in a sheet', async () => {
    await renderWithVoice(
      <>
        <VoiceProbe />
        <TypedCommandSheet />
      </>,
      { engines: {} },
    );
    expect(screen.queryByTestId(typedCommandSheetId)).not.toBeOnTheScreen();
    await act(async () => probe.voice!.typeInstead());
    expect(await screen.findByTestId(typedCommandSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(commandComposerReasonId)).toHaveTextContent(
      'Microphone is off · typing instead',
    );
    // Closing the sheet forgets the request.
    await fireEvent.press(screen.getByLabelText('Close'));
    await waitFor(() => expect(screen.queryByTestId(typedCommandSheetId)).not.toBeOnTheScreen());
  });
});
