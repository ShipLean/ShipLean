import { ToastHost } from '../../../components/ToastHost';
import { LiveDot, useConnectionState, useProjectRealtime } from '../../../realtime';
import { DecisionsSection } from '../DecisionsSection';
import type { AsksItem } from '../regions';

// The whole section as the item page hosts it, joined to the project's hub so a pushed asksChanged reaches its hooks.
// The CT mount already wraps it in a fresh QueryClient (playwright/index.tsx).
export function AsksHarness({ projectKey, item }: { projectKey: string; item: AsksItem }) {
  useProjectRealtime(projectKey);
  const connection = useConnectionState();
  return (
    <div className="p-4">
      <LiveDot state={connection} />
      <DecisionsSection item={item} />
      <ToastHost />
    </div>
  );
}
