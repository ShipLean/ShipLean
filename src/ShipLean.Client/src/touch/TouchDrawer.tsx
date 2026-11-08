import { useEffect, useRef } from 'react';
import { useModalFocus } from '../components/useModalFocus';
import { SideMenu } from '../features/shell/SideMenu/SideMenu';
import { touchTestIds as ids } from './testIds';

// The navigation drawer at 900px and below (K4): the side menu in a left sheet over the touch scrim. Close, Esc and a
// tap on the scrim close it; the shell closes it on any route change. Mount it only while open, so the side menu is in
// the page once. No edge swipe (the design rules it out).
export function TouchDrawer({ onClose }: { onClose: () => void }) {
  const panel = useRef<HTMLDivElement>(null);
  useModalFocus(panel);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || event.defaultPrevented) return;
      event.preventDefault();
      onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <>
      <div
        data-testid={ids.drawerScrim}
        aria-hidden="true"
        className="fixed inset-0 z-60 bg-(--scrim-touch)"
        onClick={onClose}
      />
      <div
        ref={panel}
        data-testid={ids.drawer}
        role="dialog"
        aria-modal="true"
        aria-label="Navigation"
        tabIndex={-1}
        className="fixed inset-y-0 left-0 z-61 flex w-[min(var(--drawer-width),86vw)] flex-col border-r border-edge bg-sunken pt-[env(safe-area-inset-top)] pb-[env(safe-area-inset-bottom)] pl-[env(safe-area-inset-left)] outline-none"
      >
        <div className="flex h-(--mobile-bar-height) flex-none items-center justify-end px-2">
          <button
            type="button"
            data-testid={ids.drawerClose}
            aria-label="Close menu"
            className="flex size-(--touch-target) items-center justify-center rounded-control border-0 bg-transparent text-ink-muted hover:text-ink"
            onClick={onClose}
          >
            <span aria-hidden="true">✕</span>
          </button>
        </div>
        <div className="flex min-h-0 flex-1 overflow-y-auto [&>aside]:min-h-full [&>aside]:w-full [&>aside]:border-r-0">
          <SideMenu />
        </div>
      </div>
    </>
  );
}
