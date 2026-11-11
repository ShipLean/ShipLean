import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { engines } from '../../voice/engines';

// While active, runs a recognition session and returns its level (0..1). failed is true when the recognizer can't
// start (no models, mic refused), so that doesn't look like silence. It cancels on blur or unmount.
export function useLiveLevel(active: boolean): { level: number; failed: boolean } {
  const [level, setLevel] = useState(0);
  const [failed, setFailed] = useState(false);
  useFocusEffect(
    useCallback(() => {
      if (!active) return undefined;
      let live = true;
      let cancel: (() => void) | null = null;
      setFailed(false);
      engines()
        .recognizer.start({ hotwords: [] })
        .then(
          (session) => {
            if (!live) {
              session.cancel();
              return;
            }
            session.onLevel((l) => {
              if (live) setLevel(l);
            });
            cancel = () => session.cancel();
          },
          () => {
            if (live) setFailed(true);
          },
        );
      return () => {
        live = false;
        cancel?.();
        setLevel(0);
      };
    }, [active]),
  );
  return { level: active ? level : 0, failed: active && failed };
}
