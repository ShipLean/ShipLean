import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client';
import Constants from 'expo-constants';
import { useMemo } from 'react';
import { currentInboxQueryStoreKey } from '../../inbox/storage';
import { createInboxPersister, inboxPersistOptions } from '../../query/persist';
import { queryClient } from '../../queryClient';
import { kvStore } from '../../session/session';
import type { AppSlotProps } from './types';

// The one query cache, with the Inbox and Answered kept on the phone (query/persist.ts).
export function QueryProvider({ children }: AppSlotProps) {
  const persistOptions = useMemo(
    () =>
      inboxPersistOptions(
        createInboxPersister(kvStore, currentInboxQueryStoreKey),
        Constants.expoConfig?.version ?? '',
      ),
    [],
  );
  return (
    <PersistQueryClientProvider client={queryClient} persistOptions={persistOptions}>
      {children}
    </PersistQueryClientProvider>
  );
}
