import type { ComponentType } from 'react';
import { ToastHost } from '../../../components/ToastHost';

// Mounts one region on its own with the props regions.ts pins, without the section's fold logic. The CT mount already
// wraps it in a fresh QueryClient (playwright/index.tsx), so a region's useAskWrites works against stubAsks.
// Usage: <RegionHarness region={DecisionList} props={{ item, asks: fixtures.waiting }} />
export function RegionHarness<P extends object>({
  region: Region,
  props,
}: {
  region: ComponentType<P>;
  props: P;
}) {
  return (
    <div className="p-4">
      <Region {...props} />
      <ToastHost />
    </div>
  );
}
