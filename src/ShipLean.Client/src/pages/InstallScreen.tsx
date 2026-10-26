import type { ReactNode } from 'react';
import { useScreenPresence } from '../realtime';
import type { RouteId } from '../routes';

interface InstallScreenProps {
  routeId: RouteId;
  testId: string;
  children: ReactNode;
}

// The root of a page that belongs to the install or to you: it reports presence (a no-op for these ids) and lays out
// its slots.
export function InstallScreen({ routeId, testId, children }: InstallScreenProps) {
  useScreenPresence(routeId);
  return (
    <div data-testid={testId} className="p-4">
      {children}
    </div>
  );
}
