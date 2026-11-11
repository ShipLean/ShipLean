import { useCallback, useEffect, useState } from 'react';
import { AppState } from 'react-native';
import { engines } from '../voice/engines';
import type { MicState } from './voiceSettingsView';

// The microphone's state, null until the phone has answered; read again when the app comes back to the front, since
// the person may have changed it in the phone's settings.
export function useMicStatus(): { mic: MicState | null; refresh: () => void } {
  const [mic, setMic] = useState<MicState | null>(null);
  const [tick, setTick] = useState(0);
  const refresh = useCallback(() => setTick((n) => n + 1), []);
  useEffect(() => {
    let live = true;
    void engines()
      .mic.status()
      .then((next) => {
        if (live) setMic(next);
      });
    return () => {
      live = false;
    };
  }, [tick]);
  useEffect(() => {
    const sub = AppState.addEventListener('change', (state) => {
      if (state === 'active') refresh();
    });
    return () => sub.remove();
  }, [refresh]);
  return { mic, refresh };
}
