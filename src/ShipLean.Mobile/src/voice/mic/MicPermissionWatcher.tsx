import { router, usePathname, useRootNavigationState } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';
import { kvStore } from '../../session/session';
import { useToast } from '../../ui/Toast';
import { engines } from '../engines';
import { cameBackOn, readLastMic, readLastRoute, writeLastMic, writeLastRoute } from './lastPlace';
import { onMicRecheck } from './permissionEvents';

const TOAST_MS = 4000;
const BACK_ON = 'Microphone on · hold to talk';

// Reads the microphone when the app comes to the front (and when the permission sheet says it was answered) and
// shows "Microphone on · hold to talk", once, when it went from off or unasked to on. The last read and the current
// screen are kept, so when the phone restarted the app after a privacy change, the toast still shows and the app
// goes back to the screen the person left. A normal foreground never navigates. Renders nothing.
export function MicPermissionWatcher() {
  const toast = useToast();
  const pathname = usePathname();
  const navigationReady = useRootNavigationState()?.key !== undefined;
  // The screen is only stored once the first read is done, so the screen kept before this launch isn't overwritten.
  const [tracking, setTracking] = useState(false);
  // Read once: the kv store is not read again on every render.
  const stored = useRef<ReturnType<typeof readLastRoute> | undefined>(undefined);
  if (stored.current === undefined) stored.current = readLastRoute(kvStore);
  // Where to go once the navigator exists, after a cold start that found the microphone back on.
  const waiting = useRef<string | null>(null);
  const latest = useRef({ toast, pathname, navigationReady });
  useEffect(() => {
    latest.current = { toast, pathname, navigationReady };
  });

  useEffect(() => {
    let live = true;
    const read = async (coldStart: boolean) => {
      const now = await engines().mic.status();
      if (!live) return;
      const back = cameBackOn(readLastMic(kvStore), now);
      writeLastMic(kvStore, now);
      if (back) {
        latest.current.toast.show({ text: BACK_ON, ms: TOAST_MS });
        if (coldStart) waiting.current = stored.current ?? null;
      }
      setTracking(true);
    };
    void read(true);
    const sub = AppState.addEventListener('change', (s) => {
      if (s === 'active') void read(false);
    });
    const off = onMicRecheck(() => void read(false));
    return () => {
      live = false;
      sub.remove();
      off();
    };
  }, []);

  useEffect(() => {
    const to = waiting.current;
    if (!tracking || to === null || !navigationReady) return;
    waiting.current = null;
    if (to !== pathname) router.replace(to);
  }, [tracking, navigationReady, pathname]);

  useEffect(() => {
    if (tracking && waiting.current === null) writeLastRoute(kvStore, pathname);
  }, [tracking, pathname]);

  return null;
}
