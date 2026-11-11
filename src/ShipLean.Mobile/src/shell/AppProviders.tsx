import type { ReactNode } from 'react';
import { MicProvider } from './providers/mic';
import { QueryProvider } from './providers/query';

// The app-wide provider slots, outermost first. Each slot's owner replaces only its slot file.
export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryProvider>
      <MicProvider>{children}</MicProvider>
    </QueryProvider>
  );
}
