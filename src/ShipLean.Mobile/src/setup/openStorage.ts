import { Linking, Platform } from 'react-native';

// Android has a page for the phone's storage; iPhone only opens ShipLean's own page in Settings, where its storage
// is listed under General.
export async function openPhoneStorage(): Promise<void> {
  if (Platform.OS === 'android') {
    await Linking.sendIntent('android.settings.INTERNAL_STORAGE_SETTINGS');
    return;
  }
  await Linking.openSettings();
}
