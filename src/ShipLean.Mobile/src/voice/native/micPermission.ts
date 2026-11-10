// The microphone permission through expo-audio's recording permission.
import type { MicPermission } from '../contracts';

export class ExpoMicPermission implements MicPermission {
  status(): Promise<'granted' | 'undetermined' | 'deniedCanAsk' | 'deniedFinal'> {
    // TODO: map expo-audio's { status, granted, canAskAgain } to the four statuses.
    return Promise.reject(new Error('TODO: the microphone permission status'));
  }

  request(): Promise<boolean> {
    // TODO: ask with expo-audio's requestRecordingPermissionsAsync.
    return Promise.reject(new Error('TODO: ask for the microphone'));
  }

  openSettings(): void {
    // TODO: open the app's page in the phone's settings.
    throw new Error('TODO: open the phone settings');
  }
}
