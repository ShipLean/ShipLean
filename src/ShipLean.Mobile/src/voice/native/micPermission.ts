// The microphone permission through expo-audio's recording permission.
import { getRecordingPermissionsAsync, requestRecordingPermissionsAsync } from 'expo-audio';
import { Linking } from 'react-native';
import type { MicPermission } from '../contracts';
import { permissionState } from '../mic/permissionState';

export class ExpoMicPermission implements MicPermission {
  async status(): Promise<'granted' | 'undetermined' | 'deniedCanAsk' | 'deniedFinal'> {
    return permissionState(await getRecordingPermissionsAsync());
  }

  // Asks only while the phone can still show its prompt; a refusal it won't ask about again needs the settings.
  async request(): Promise<boolean> {
    const now = await getRecordingPermissionsAsync();
    if (now.granted) return true;
    if (!now.canAskAgain) return false;
    const answer = await requestRecordingPermissionsAsync();
    return answer.granted;
  }

  openSettings(): void {
    void Linking.openSettings();
  }
}
