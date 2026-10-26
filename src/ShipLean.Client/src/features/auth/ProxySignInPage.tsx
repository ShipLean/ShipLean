import { useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';
import { fetchMe, meKey } from '../../queries/shared';
import { Banner } from './Banner';
import { authTestIds } from './testIds';

const messages = {
  none: "Sign-in is handled by your organisation's sign-in proxy, and no sign-in reached ShipLean. Open ShipLean through its usual address.",
  disabled: 'Your ShipLean account is disabled. Ask an instance admin to turn it back on.',
  ended:
    'Your session ended. Reload the page to sign in again through your proxy. If it keeps happening, ask an instance admin.',
};

// /login in proxy mode: there is nothing to type. Once the proxy's identity has arrived /api/me answers 200 and the
// person goes on to where they were heading; otherwise one message says why they are here. "signed-out" and "change"
// (password-mode states) read as no state.
export function ProxySignInPage({
  state,
  instanceName,
  onSignedIn,
  onTryAgain,
}: {
  state: string | null;
  instanceName: string | undefined;
  onSignedIn: () => void;
  onTryAgain: () => void;
}) {
  const disabled = state === 'disabled';
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe, retry: false, enabled: !disabled });

  useEffect(() => {
    if (me.data) onSignedIn();
    // onSignedIn is a fresh closure each render; only the answer should trigger it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [me.data]);

  // Waiting on /api/me, or about to leave: show nothing rather than a message that is about to be wrong.
  if (!disabled && (me.isPending || me.data)) return null;

  const message = disabled ? messages.disabled : state === 'ended' ? messages.ended : messages.none;

  return (
    <div
      data-testid={authTestIds.proxySignIn}
      className="flex w-[min(380px,100%)] flex-col rounded-frame border border-edge bg-panel shadow-panel"
    >
      <h1 className="m-0 border-b border-edge px-4 py-3 text-[length:var(--text-md)] font-semibold">
        Sign in
        {instanceName ? (
          <small className="mt-0.5 block text-[length:var(--text-xs)] font-normal text-ink-muted">
            to {instanceName}
          </small>
        ) : null}
      </h1>
      <div className="grid gap-3 p-4">
        <Banner tone="info" testId={authTestIds.proxyMessage}>
          {message}
        </Banner>
      </div>
      <footer className="flex items-center justify-end gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3">
        <button
          type="button"
          data-testid={authTestIds.proxyRetry}
          className="btn-signal"
          onClick={onTryAgain}
        >
          Try again
        </button>
      </footer>
    </div>
  );
}
