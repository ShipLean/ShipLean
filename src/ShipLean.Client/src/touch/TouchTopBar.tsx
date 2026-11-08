import { Link } from 'react-router-dom';
import { useQuickSearch } from '../features/quickSearch/host/useQuickSearch';
import { Crumbs } from '../features/shell/TopBar/Crumbs';
import { TopBarRealtime } from '../shell/slots/TopBarRealtime';
import { touchTestIds as ids } from './testIds';
import { useTopBarSlots } from './TopBar';

const control =
  'flex size-(--touch-target) flex-none items-center justify-center rounded-control border-0 bg-transparent text-ink-muted hover:text-ink';

// The touch shell's one pinned bar (K4), at 900px and below. Without `back`: Menu · title · presence and Live · action ·
// Search. With `back` (C13) the back link takes the left and Menu moves to the right. `pinned` (rule 12) replaces it.
export function TouchTopBar({ menuOpen, onMenu }: { menuOpen: boolean; onMenu: () => void }) {
  const slots = useTopBarSlots();
  const quickSearch = useQuickSearch();
  const frame =
    'sticky top-0 z-30 w-full flex-none border-b border-edge bg-panel pt-[env(safe-area-inset-top)]';

  if (slots.pinned !== undefined) {
    return (
      <header data-testid={ids.topBarPinned} className={frame}>
        <div className="flex h-(--mobile-bar-height) items-center gap-1 px-2">{slots.pinned}</div>
      </header>
    );
  }

  const menu = (
    <button
      type="button"
      data-testid={ids.topBarMenu}
      aria-label="Menu"
      aria-expanded={menuOpen}
      className={control}
      onClick={onMenu}
    >
      <svg
        aria-hidden="true"
        viewBox="0 0 16 16"
        className="size-4"
        fill="none"
        stroke="currentColor"
      >
        <path d="M2 4h12M2 8h12M2 12h12" strokeWidth="1.6" strokeLinecap="round" />
      </svg>
    </button>
  );

  return (
    <header data-testid={ids.topBar} className={frame}>
      <div className="flex h-(--mobile-bar-height) items-center gap-1 px-2">
        {slots.back ? (
          <Link
            data-testid={ids.topBarBack}
            to={slots.back.to}
            aria-label={slots.back.label}
            className={`${control} text-[length:var(--text-lg)] no-underline`}
          >
            <span aria-hidden="true">‹</span>
          </Link>
        ) : (
          menu
        )}
        <div
          data-testid={ids.topBarTitle}
          className="flex min-w-0 flex-1 items-center overflow-hidden whitespace-nowrap"
        >
          {slots.title ?? <Crumbs />}
        </div>
        <TopBarRealtime />
        {slots.action !== undefined ? (
          <div data-testid={ids.topBarAction} className="flex flex-none items-center">
            {slots.action}
          </div>
        ) : null}
        <button
          type="button"
          data-testid={ids.topBarSearch}
          aria-label="Quick search"
          className={control}
          onClick={quickSearch.open}
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 16 16"
            className="size-4"
            fill="none"
            stroke="currentColor"
          >
            <circle cx="7" cy="7" r="4.5" strokeWidth="1.6" />
            <path d="m10.5 10.5 3.5 3.5" strokeWidth="1.6" strokeLinecap="round" />
          </svg>
        </button>
        {slots.back ? menu : null}
      </div>
    </header>
  );
}
