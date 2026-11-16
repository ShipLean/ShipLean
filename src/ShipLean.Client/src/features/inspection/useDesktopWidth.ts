import { useSyncExternalStore } from 'react';

// Agent runs are desktop only (D15): from this width up the section and the run page show; under it they do not.
export const DESKTOP_MIN_WIDTH = 900;
const QUERY = `(min-width: ${DESKTOP_MIN_WIDTH}px)`;

function subscribe(onChange: () => void) {
  const list = window.matchMedia(QUERY);
  list.addEventListener('change', onChange);
  return () => list.removeEventListener('change', onChange);
}

export function useDesktopWidth(): boolean {
  return useSyncExternalStore(subscribe, () => window.matchMedia(QUERY).matches);
}
