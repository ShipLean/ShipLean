import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { engines } from '../../voice/engines';

// While active, runs a recognition session and returns its level (0..1). It cancels on blur or unmount.
export function useLiveLevel(active: boolean): number {
  const [level, setLevel] = useState(0);
  useFocusEffect(
    useCallback(() => {
      if (!active) return undefined;
      let live = true;
      let cancel: (() => void) | null = null;
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
          () => undefined,
        );
      return () => {
        live = false;
        cancel?.();
        setLevel(0);
      };
    }, [active]),
  );
  return active ? level : 0;
}
