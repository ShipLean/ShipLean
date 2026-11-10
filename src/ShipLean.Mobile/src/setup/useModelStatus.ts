import { useCallback, useSyncExternalStore } from 'react';
import type { ModelStatus } from '../voice/contracts';
import { engines } from '../voice/engines';

// The model store's status, re-read when the store says it changed.
export function useModelStatus(): ModelStatus {
  const models = engines().models;
  const subscribe = useCallback((cb: () => void) => models.subscribe(cb), [models]);
  return useSyncExternalStore(subscribe, () => models.status());
}
