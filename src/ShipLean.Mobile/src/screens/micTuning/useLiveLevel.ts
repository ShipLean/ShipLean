import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { engines } from '../../voice/engines';

// While active, watches the recognizer's level-only stream (shared by every watcher, paused while a recognition
// session runs) and returns its level (0..1). failed is true when the stream can't start (mic refused), so that
// doesn't look like silence. It stops watching on blur or unmount.
export function useLiveLevel(active: boolean): { level: number; failed: boolean } {
  const [level, setLevel] = useState(0);
  const [failed, setFailed] = useState(false);
  useFocusEffect(
    useCallback(() => {
      if (!active) return undefined;
      setFailed(false);
      const stop = engines().recognizer.watchLevel(setLevel, () => setFailed(true));
      return () => {
        stop();
        setLevel(0);
      };
    }, [active]),
  );
  return { level: active ? level : 0, failed: active && failed };
}
