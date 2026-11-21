import { useEffect, useState } from 'react';
import { engines } from '../voice/engines';

// How often to look again while the speech engine is still loading.
export const ENGINE_POLL_MS = 1000;

// Whether the speech recognizer is loaded and can start. The model files being checked (phase 'ready') isn't enough:
// the engine loads after that and can fail (SHIP-1459). The recognizer has no change event, so this looks again every
// second until it is ready, then stops.
export function useEngineReady(): boolean {
  const [ready, setReady] = useState(() => engines().recognizer.ready());
  useEffect(() => {
    if (ready) return;
    const id = setInterval(() => {
      if (engines().recognizer.ready()) setReady(true);
    }, ENGINE_POLL_MS);
    return () => clearInterval(id);
  }, [ready]);
  return ready;
}
