import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from '../../queryClient';
import type { AppSlotProps } from './types';

// TODO: a PersistQueryClientProvider with the sync storage persister replaces the plain provider.
export function QueryProvider({ children }: AppSlotProps) {
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
