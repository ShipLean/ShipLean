import { useSyncExternalStore } from 'react';

// The Changes section and page need a screen wider than 900px (design D2).
const QUERY = '(min-width: 901px)';

function subscribe(listener: () => void): () => void {
  const list = window.matchMedia(QUERY);
  list.addEventListener('change', listener);
  return () => list.removeEventListener('change', listener);
}

export function useWideScreen(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(QUERY).matches,
    () => true,
  );
}
