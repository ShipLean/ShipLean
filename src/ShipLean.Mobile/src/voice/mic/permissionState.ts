import type { MicStatus } from '../internals';

// What expo-audio's recording permission reports.
export interface RecordingPermission {
  granted: boolean;
  status: 'granted' | 'denied' | 'undetermined';
  canAskAgain: boolean;
}

// The four states the app works with. A refusal the phone will still ask about again is not final: only a refusal
// with canAskAgain false needs the phone's settings.
export function permissionState(p: RecordingPermission): MicStatus {
  if (p.granted || p.status === 'granted') return 'granted';
  if (p.status === 'undetermined') return 'undetermined';
  return p.canAskAgain ? 'deniedCanAsk' : 'deniedFinal';
}
