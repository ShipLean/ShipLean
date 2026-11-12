// The local model-files module: what expo-file-system can't do for the voice model files. Only iOS has a per-file
// backup flag; on Android the call does nothing, because the app's backup is off (app.json). Only Android has the
// download's foreground service; iOS downloads in a background URLSession instead.
import { requireNativeModule } from 'expo';
import { Platform } from 'react-native';

interface ModelFilesNative {
  excludeFromBackup(uri: string): Promise<void>;
  startDownloadService?(percent: number): boolean;
  stopDownloadService?(): void;
}

const native = requireNativeModule<ModelFilesNative>('ShipLeanModelFiles');

// Keeps a file or a directory (with its contents) out of iCloud backup.
export function excludeFromBackup(uri: string): Promise<void> {
  return native.excludeFromBackup(uri);
}

// Android: starts the download's foreground service, or updates its notification to this percentage (-1: not known
// yet). False when Android refused to start it (the app wasn't in the foreground). iOS: does nothing.
export function startDownloadService(percent: number): boolean {
  if (Platform.OS !== 'android') return true;
  return native.startDownloadService?.(percent) ?? false;
}

export function stopDownloadService(): void {
  if (Platform.OS !== 'android') return;
  native.stopDownloadService?.();
}
