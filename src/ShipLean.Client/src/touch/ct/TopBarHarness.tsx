import { useState } from 'react';
import { QuickSearchProvider } from '../../features/quickSearch/host/QuickSearchProvider';
import { TopBarProvider, useTopBar } from '../TopBar';
import { TouchTopBar } from '../TouchTopBar';
import { topBarHarnessIds as ids } from './topBarHarnessIds';

export type TopBarHarnessVariant = 'plain' | 'back' | 'pinned';

// The touch top bar as the shell mounts it, with a page that sets its slots and is taller than the screen. Mount it
// on a project route with the shell stubbed, so the breadcrumb and the Live dot render.
export function TopBarHarness({ variant }: { variant: TopBarHarnessVariant }) {
  const [menuOpen, setMenuOpen] = useState(false);
  return (
    <QuickSearchProvider>
      <TopBarProvider>
        <div className="flex min-h-dvh flex-col bg-canvas text-ink">
          <TouchTopBar menuOpen={menuOpen} onMenu={() => setMenuOpen(!menuOpen)} />
          <HarnessPage variant={variant} />
        </div>
      </TopBarProvider>
    </QuickSearchProvider>
  );
}

function HarnessPage({ variant }: { variant: TopBarHarnessVariant }) {
  useTopBar({
    back: variant === 'plain' ? undefined : { to: '/p/SHIP/backlogs/stories', label: 'Back to Stories' },
    title: variant === 'plain' ? undefined : 'SHIP-101',
    action: (
      <button
        type="button"
        data-testid={ids.ctTopBarActionButton}
        className="h-(--touch-target) rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)]"
      >
        Edit
      </button>
    ),
    pinned:
      variant === 'pinned' ? (
        <button
          type="button"
          data-testid={ids.ctTopBarPinnedDone}
          className="h-(--touch-target) rounded-control border border-edge bg-panel px-3"
        >
          Done
        </button>
      ) : undefined,
  });
  return (
    <main data-testid={ids.ctTopBarContent} className="grid gap-2 p-4">
      {Array.from({ length: 80 }, (_, row) => (
        <p key={row} className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          Row {row + 1}
        </p>
      ))}
    </main>
  );
}
