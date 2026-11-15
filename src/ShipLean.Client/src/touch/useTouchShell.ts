import { useSyncExternalStore } from 'react';
import { PHONE_QUERY, TOUCH_QUERY, type Viewport } from './viewport';

export interface TouchShell {
  /** True at 900px and below, where the touch layout applies. */
  touch: boolean;
  viewport: Viewport;
}

function subscribe(onChange: () => void): () => void {
  const queries = [window.matchMedia(TOUCH_QUERY), window.matchMedia(PHONE_QUERY)];
  for (const query of queries) query.addEventListener('change', onChange);
  return () => {
    for (const query of queries) query.removeEventListener('change', onChange);
  };
}

// A string snapshot, so useSyncExternalStore compares by value and doesn't loop on a fresh object each read.
function snapshot(): Viewport {
  if (window.matchMedia(PHONE_QUERY).matches) return 'phone';
  return window.matchMedia(TOUCH_QUERY).matches ? 'tablet' : 'desktop';
}

/** Whether the touch layout applies (900px and below) and the viewport class, kept current as the window resizes. */
export function useTouchShell(): TouchShell {
  const viewport = useSyncExternalStore(subscribe, snapshot);
  return { touch: viewport !== 'desktop', viewport };
}
