import { getRecordingPermissionsAsync, requestRecordingPermissionsAsync } from 'expo-audio';
import { Linking } from 'react-native';
import { native, setMicPermission } from '../../test-support/nativeMocks';
import { ExpoMicPermission } from './micPermission';

describe('ExpoMicPermission', () => {
  it('reads the four states from the phone', async () => {
    const mic = new ExpoMicPermission();
    setMicPermission({ status: 'denied', granted: false, canAskAgain: false });
    expect(await mic.status()).toBe('deniedFinal');
    setMicPermission({ status: 'denied', granted: false, canAskAgain: true });
    expect(await mic.status()).toBe('deniedCanAsk');
    expect(getRecordingPermissionsAsync).toHaveBeenCalled();
  });

  it('request asks the phone and returns its answer', async () => {
    setMicPermission({ status: 'undetermined', granted: false, canAskAgain: true });
    const mic = new ExpoMicPermission();
    // The person taps Allow in the phone's prompt.
    jest.mocked(requestRecordingPermissionsAsync).mockImplementationOnce(async () => {
      native.micRequests += 1;
      setMicPermission({ status: 'granted', granted: true, canAskAgain: true });
      return getRecordingPermissionsAsync();
    });
    expect(await mic.request()).toBe(true);
    expect(native.micRequests).toBe(1);
  });

  it('request is never called when canAskAgain is false', async () => {
    setMicPermission({ status: 'denied', granted: false, canAskAgain: false });
    expect(await new ExpoMicPermission().request()).toBe(false);
    expect(native.micRequests).toBe(0);
  });

  it('openSettings calls Linking.openSettings', () => {
    const open = jest.spyOn(Linking, 'openSettings').mockResolvedValue();
    new ExpoMicPermission().openSettings();
    expect(open).toHaveBeenCalledTimes(1);
  });
});
