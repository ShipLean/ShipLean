import type { ReactNode } from 'react';
import { ToastHost } from '../../../components/ToastHost';

// Mounts one region on its own with the props regions.ts pins, without the section's fold logic. The CT mount already
// wraps it in a fresh QueryClient (playwright/index.tsx), so a region's useAskWrites works against stubAsks.
// The region goes in as a child: Playwright CT turns an import into a browser reference only when the spec uses it as a
// JSX tag, so passing a component as a prop would arrive as a Node-side callback.
// Usage: <RegionHarness><DecisionList item={item} asks={fixtures.waiting} /></RegionHarness>
export function RegionHarness({ children }: { children: ReactNode }) {
  return (
    <div className="p-4">
      {children}
      <ToastHost />
    </div>
  );
}
