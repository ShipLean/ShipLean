import { useEffect } from 'react';
import { useQueryClient, type QueryKey } from '@tanstack/react-query';
import type { BacklogViewDto } from '../../../api/views';
import { showToast } from '../../../components/toastStore';
import { onReconnected } from '../../../realtime';
import { changesText, countChanges } from './countChanges';

/**
 * After a reconnect the shell refetches every query of the project. When the backlog on screen has been read again,
 * says how many items changed while the connection was down (compared with what was shown before).
 */
export function useReconnectSummary(queryKey: QueryKey) {
  const queryClient = useQueryClient();
  const serialised = JSON.stringify(queryKey);
  useEffect(() => {
    const key = JSON.parse(serialised) as QueryKey;
    let unsubscribe: (() => void) | undefined;
    const stop = onReconnected(() => {
      const before = queryClient.getQueryData<BacklogViewDto>(key);
      const readAt = queryClient.getQueryState(key)?.dataUpdatedAt ?? 0;
      if (before === undefined) return;
      const reconnectedAt = Date.now();
      unsubscribe?.();
      unsubscribe = queryClient.getQueryCache().subscribe(() => {
        const state = queryClient.getQueryState<BacklogViewDto>(key);
        // The read failed: there is nothing to compare, and the next reconnect starts afresh.
        if (state?.fetchStatus === 'idle' && state.errorUpdatedAt >= reconnectedAt) {
          unsubscribe?.();
          unsubscribe = undefined;
          return;
        }
        if (
          state?.data === undefined ||
          state.fetchStatus !== 'idle' ||
          state.dataUpdatedAt <= readAt
        )
          return;
        unsubscribe?.();
        unsubscribe = undefined;
        showToast(changesText(countChanges(before.rows, state.data.rows)), { tone: 'ok' });
      });
    });
    return () => {
      stop();
      unsubscribe?.();
    };
  }, [serialised, queryClient]);
}
