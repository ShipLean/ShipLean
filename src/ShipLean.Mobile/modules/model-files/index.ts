// The local model-files module: what expo-file-system can't do for the voice model files. Only iOS has a per-file
// backup flag; on Android the call does nothing, because the app's backup is off (app.json).
import { requireNativeModule } from 'expo';

interface ModelFilesNative {
  excludeFromBackup(uri: string): Promise<void>;
}

const native = requireNativeModule<ModelFilesNative>('ShipLeanModelFiles');

// Keeps a file or a directory (with its contents) out of iCloud backup.
export function excludeFromBackup(uri: string): Promise<void> {
  return native.excludeFromBackup(uri);
}
