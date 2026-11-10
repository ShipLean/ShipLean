import { act, fireEvent, screen } from '@testing-library/react-native';
import { Platform } from 'react-native';
import { useState } from 'react';
import { renderScreen } from '../../test-support/renderScreen';
import { installFakeEngines } from '../../test-support/voiceUi';
import type { MicStatus } from '../internals';
import { onMicRecheck } from '../mic/permissionEvents';
import { MicPermissionSheet } from './MicPermissionSheet';
import { micPermissionIds, micPermissionSheetId } from './MicPermissionSheet.testIds';

// Holds open like MicSlot does, and records what the sheet's buttons asked for.
function Host({ status, typed }: { status: MicStatus; typed: string[] }) {
  const [open, setOpen] = useState(true);
  return (
    <MicPermissionSheet
      open={open}
      status={status}
      onClose={() => setOpen(false)}
      onTypeInstead={() => typed.push('typeInstead')}
    />
  );
}

const mount = (status: MicStatus) => {
  const typed: string[] = [];
  return { typed, shown: renderScreen(<Host status={status} typed={typed} />) };
};

afterEach(() => jest.restoreAllMocks());

describe('MicPermissionSheet', () => {
  it('undetermined shows Allow microphone and the nothing-is-kept promise, and a yes closes the sheet', async () => {
    const { mic } = installFakeEngines();
    mic.set('undetermined');
    const rechecks: string[] = [];
    onMicRecheck(() => rechecks.push('recheck'));
    await mount('undetermined').shown;

    expect(screen.getByTestId(micPermissionIds.formAsk)).toBeOnTheScreen();
    expect(screen.getByTestId(micPermissionIds.promises)).toHaveTextContent(
      /Nothing you say is kept; only the text you approve is posted/,
    );
    expect(screen.queryByText(/clip|recording/i)).toBeNull();
    expect(screen.getByTestId(micPermissionIds.allow)).toHaveTextContent('Allow microphone');
    expect(screen.getByTestId(micPermissionIds.typeInstead)).toBeOnTheScreen();
    expect(screen.getByTestId(micPermissionIds.note)).toHaveTextContent(
      'Everything else works without voice',
    );
    expect(mic.requests).toBe(0);

    await fireEvent.press(screen.getByTestId(micPermissionIds.allow));
    expect(mic.requests).toBe(1);
    expect(screen.queryByTestId(micPermissionSheetId)).toBeNull();
    // The watcher is told to read again, which shows the toast.
    expect(rechecks).toEqual(['recheck']);
  });

  it('deniedFinal on iOS shows the Settings drawing and Open Settings', async () => {
    jest.replaceProperty(Platform, 'OS', 'ios');
    const { mic } = installFakeEngines();
    await mount('deniedFinal').shown;

    expect(screen.getByTestId(micPermissionIds.formIos)).toBeOnTheScreen();
    expect(screen.getByTestId(micPermissionIds.settingsDrawing)).toBeOnTheScreen();
    expect(screen.queryByTestId(micPermissionIds.appInfoDrawing)).toBeNull();
    expect(screen.getByTestId(micPermissionIds.markedRow)).toHaveTextContent(/^MicrophoneTurn on$/);
    expect(screen.getByTestId(micPermissionIds.lead)).toHaveTextContent(
      'iPhone won’t ask again, so it’s one switch in Settings.',
    );
    expect(screen.queryByTestId(micPermissionIds.allow)).toBeNull();
    expect(screen.getByTestId(micPermissionIds.note)).toHaveTextContent(
      'Everything else works without voice',
    );

    await fireEvent.press(screen.getByTestId(micPermissionIds.openSettings));
    expect(mic.settingsOpened).toBe(1);
    expect(mic.requests).toBe(0);
  });

  it('deniedFinal on Android shows the app info drawing and App settings', async () => {
    jest.replaceProperty(Platform, 'OS', 'android');
    const { mic } = installFakeEngines();
    await mount('deniedFinal').shown;

    expect(screen.getByTestId(micPermissionIds.formAndroid)).toBeOnTheScreen();
    expect(screen.getByTestId(micPermissionIds.appInfoDrawing)).toBeOnTheScreen();
    expect(screen.queryByTestId(micPermissionIds.settingsDrawing)).toBeNull();
    expect(screen.getByTestId(micPermissionIds.markedRow)).toHaveTextContent(
      /^Allow only while using the appPick this$/,
    );
    expect(screen.getByTestId(micPermissionIds.appSettings)).toHaveTextContent('App settings');

    await fireEvent.press(screen.getByTestId(micPermissionIds.appSettings));
    expect(mic.settingsOpened).toBe(1);
  });

  it('a no after Allow microphone turns the sheet into the denied form', async () => {
    jest.replaceProperty(Platform, 'OS', 'ios');
    const { mic } = installFakeEngines();
    mic.set('undetermined');
    mic.answerRequests(false);
    // iPhone asks once: after a no the phone reads as final.
    jest.spyOn(mic, 'status').mockResolvedValue('deniedFinal');
    await mount('undetermined').shown;

    await fireEvent.press(screen.getByTestId(micPermissionIds.allow));
    expect(mic.requests).toBe(1);
    expect(screen.getByTestId(micPermissionSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(micPermissionIds.formIos)).toBeOnTheScreen();
    expect(screen.queryByTestId(micPermissionIds.allow)).toBeNull();
    expect(screen.getByTestId(micPermissionIds.openSettings)).toBeOnTheScreen();
  });

  it('a first no on Android still offers one more ask', async () => {
    jest.replaceProperty(Platform, 'OS', 'android');
    const { mic } = installFakeEngines();
    mic.set('undetermined');
    mic.answerRequests(false);
    await mount('undetermined').shown;

    await fireEvent.press(screen.getByTestId(micPermissionIds.allow));
    expect(screen.getByTestId(micPermissionIds.formAsk)).toBeOnTheScreen();
    expect(screen.getByTestId(micPermissionIds.allow)).toBeOnTheScreen();
  });

  it('Type instead calls typeInstead and closes the sheet', async () => {
    const { mic } = installFakeEngines();
    const { typed, shown } = mount('deniedFinal');
    await shown;
    await fireEvent.press(screen.getByTestId(micPermissionIds.typeInstead));
    expect(typed).toEqual(['typeInstead']);
    expect(screen.queryByTestId(micPermissionSheetId)).toBeNull();
    expect(mic.requests).toBe(0);
    expect(mic.settingsOpened).toBe(0);
  });

  it('shows no banner or dot of its own when closed', async () => {
    installFakeEngines();
    const view = await renderScreen(
      <MicPermissionSheet
        open={false}
        status="deniedFinal"
        onClose={() => {}}
        onTypeInstead={() => {}}
      />,
    );
    expect(screen.queryByTestId(micPermissionSheetId)).toBeNull();
    await act(async () => view.unmount());
  });
});
