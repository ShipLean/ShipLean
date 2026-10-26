import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { Link } from 'react-router-dom';
import { Avatar } from '../../../components/Avatar';
import { Chip } from '../../../components/Chip';
import { showToast } from '../../../components/toastStore';
import { useSignInMode } from '../../auth/useSignInMode';
import { routePath } from '../../../routes';
import { signOut } from '../shellApi';
import { shellTestIds as ids } from '../testIds';
import { useMe } from '../useShellQueries';

const itemClass =
  'flex h-[30px] items-center gap-2 rounded-control px-2 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft focus-visible:bg-signal-soft focus-visible:outline-0';

// The signed-in user at the bottom of the side menu, and the menu it opens upwards: who you are, Your profile,
// Instance settings (instance admins only) and Sign out.
export function UserMenu() {
  const meQuery = useMe();
  const me = meQuery.data;
  const [open, setOpen] = useState(false);
  const zone = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  const { proxy, signOutUrl } = useSignInMode();
  const leave = useMutation({
    mutationFn: signOut,
    // A full load, so nothing of this session stays in the page. In proxy mode the proxy ends its own session too.
    onSuccess: () =>
      window.location.assign(proxy && signOutUrl ? signOutUrl : '/login?state=signed-out'),
    onError: () => showToast('Could not sign out. Try again.', { tone: 'danger' }),
  });

  useEffect(() => {
    if (!open) return;
    menu.current?.querySelector<HTMLElement>('[role="menuitem"]')?.focus();
    const close = (event: MouseEvent) => {
      if (!zone.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [open]);

  if (meQuery.isError) {
    return (
      <button
        type="button"
        data-testid={ids.userRetry}
        className="mt-auto flex w-full items-center gap-2 border-0 border-t border-edge bg-transparent p-2 text-left text-[length:var(--text-sm)] text-alert hover:bg-panel/60 max-[900px]:mt-0 max-[900px]:ml-auto max-[900px]:w-auto max-[900px]:border-t-0"
        onClick={() => void meQuery.refetch()}
      >
        Could not load your account. Retry
      </button>
    );
  }
  if (!me) return null;

  function onKeyDown(event: KeyboardEvent) {
    const items = [...(menu.current?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])];
    const at = items.indexOf(document.activeElement as HTMLElement);
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      items[(at + step + items.length) % items.length]?.focus();
    } else if (event.key === 'Escape') {
      setOpen(false);
      button.current?.focus();
    }
  }

  return (
    <div ref={zone} className="contents">
      <button
        ref={button}
        type="button"
        data-testid={ids.userButton}
        aria-haspopup="menu"
        aria-expanded={open}
        className="mt-auto flex w-full items-center gap-2 border-0 border-t border-edge bg-transparent p-2 text-left text-[length:var(--text-sm)] hover:bg-panel/60 max-[900px]:mt-0 max-[900px]:ml-auto max-[900px]:w-auto max-[900px]:border-t-0"
        onClick={() => setOpen(!open)}
      >
        <Avatar {...me} />
        <span className="min-w-0 flex-1 truncate max-[900px]:hidden">{me.name}</span>
        <span className="text-ink-muted max-[900px]:hidden" aria-hidden="true">
          ▴
        </span>
      </button>
      {open ? (
        <div
          ref={menu}
          role="menu"
          aria-label="Your account"
          data-testid={ids.userMenu}
          className="absolute right-2 bottom-12 left-2 z-50 grid gap-px rounded-panel border border-edge bg-panel p-1 shadow-[0_12px_32px_rgb(0_0_0/0.18)] max-[900px]:top-[calc(100%-4px)] max-[900px]:right-2 max-[900px]:bottom-auto max-[900px]:left-auto max-[900px]:w-60"
          onKeyDown={onKeyDown}
        >
          <div className="mb-0.5 flex items-center gap-2 border-b border-edge px-2 pt-1.5 pb-2">
            <Avatar {...me} />
            <span className="grid min-w-0 leading-tight">
              <b
                data-testid={ids.userMenuName}
                className="text-[length:var(--text-sm)] font-semibold"
              >
                {me.name}
              </b>
              <span
                data-testid={ids.userMenuEmail}
                className="truncate text-[length:var(--text-xs)] text-ink-muted"
              >
                {me.email}
              </span>
            </span>
          </div>
          <Link
            role="menuitem"
            to={routePath('profile', {})}
            data-testid={ids.userMenuProfile}
            className={itemClass}
            onClick={() => setOpen(false)}
          >
            Your profile
          </Link>
          {me.isInstanceAdmin ? (
            <Link
              role="menuitem"
              to={routePath('instanceSettings', { tab: 'general' })}
              data-testid={ids.userMenuInstance}
              className={itemClass}
              onClick={() => setOpen(false)}
            >
              Instance settings
              <span className="ml-auto">
                <Chip>Admin</Chip>
              </span>
            </Link>
          ) : null}
          {proxy && !signOutUrl ? null : (
            <>
              <hr className="my-0.5 border-0 border-t border-edge" />
              <button
                type="button"
                role="menuitem"
                data-testid={ids.userMenuSignOut}
                className={`${itemClass} w-full text-left`}
                onClick={() => leave.mutate()}
              >
                Sign out
              </button>
            </>
          )}
        </div>
      ) : null}
    </div>
  );
}
