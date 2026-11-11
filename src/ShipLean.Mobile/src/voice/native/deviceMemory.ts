import * as Device from 'expo-device';

// The phone's memory in bytes as it reports it, or 0 when it doesn't say. manifest.ts turns it into GB.
export function deviceMemoryBytes(): number {
  return Device.totalMemory ?? 0;
}
