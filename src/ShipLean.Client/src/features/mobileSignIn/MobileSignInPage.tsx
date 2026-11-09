import { useMutation, useQuery } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { createSignInCode } from '@shiplean/core/api/deviceSessions';
import { appRedirect, parseConfirmQuery, type ConfirmQuery } from '@shiplean/core/handoff';
import { fetchMe, meKey } from '../../queries/shared';
import { useScreenView } from '../../realtime';
import { WarnCallout } from '../agents/authorize/WarnCallout';
import { fetchInstance, instanceKey } from '../auth/authApi';
import { Banner } from '../auth/Banner';
import { LoginTopBar } from '../auth/LoginTopBar';
import { Facts } from './Facts';
import { openApp } from './openApp';
import { mobileSignInTestIds as ids } from './testIds';

const unreachable = "Couldn't reach ShipLean. Check your connection and try again.";
const sub = 'The ShipLean app wants to sign in as you';
const fullWidth = 'h-10 w-full text-[length:15px]';

function Card({
  heading,
  subtitle,
  testId = ids.card,
  footer,
  children,
}: {
  heading: string;
  subtitle?: string;
  testId?: string;
  footer?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section
      data-testid={testId}
      className="flex w-[min(440px,100%)] flex-col rounded-frame border border-edge bg-panel shadow-panel"
    >
      <h1
        data-testid={ids.heading}
        className="m-0 border-b border-edge p-4 text-[length:var(--text-md)] leading-[1.3] font-semibold [overflow-wrap:anywhere]"
      >
        {heading}
        {subtitle ? (
          <small className="mt-0.5 block text-[length:var(--text-sm)] font-normal text-ink-muted">
            {subtitle}
          </small>
        ) : null}
      </h1>
      <div className="grid gap-4 p-4">{children}</div>
      {footer ? (
        <footer className="grid gap-3 rounded-b-frame border-t border-edge bg-canvas px-4 pt-3 pb-4">
          {footer}
        </footer>
      ) : null}
    </section>
  );
}

function Note({ children }: { children: ReactNode }) {
  return (
    <span className="text-center text-[length:var(--text-xs)] text-ink-muted">{children}</span>
  );
}

function Empty({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="grid justify-items-center gap-2 px-2 py-4 text-center">
      <h2 className="m-0 text-[length:var(--text-md)] font-semibold [overflow-wrap:anywhere]">
        {title}
      </h2>
      {children}
    </div>
  );
}

type Outcome = { kind: 'signedIn'; url: string } | { kind: 'cancelled' } | null;

// /mobile/sign-in: the page the phone app opens in the system browser. Signed in, it asks whether to sign the app in,
// and either posts a code and hands the browser back to the app, or says why not. It stands outside the app shell.
export function MobileSignInPage() {
  useScreenView('mobileSignIn', null);
  const { search } = useLocation();
  const query = parseConfirmQuery(search);
  const instance = useQuery({ queryKey: instanceKey, queryFn: fetchInstance, retry: false });
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe, retry: false });
  const [outcome, setOutcome] = useState<Outcome>(null);
  const host = window.location.host;

  const post = useMutation({
    mutationFn: (q: ConfirmQuery) =>
      createSignInCode({ challenge: q.challenge, deviceName: q.device }),
    onSuccess: ({ code }, q) => {
      const url = appRedirect({ code, state: q.state });
      setOutcome({ kind: 'signedIn', url });
      openApp(url);
    },
  });

  function body() {
    if ('error' in query) {
      return (
        <Card
          heading="Sign in the app"
          testId={ids.missing}
          footer={<Note>If you were signing in, go back to the app and tap Sign in again.</Note>}
        >
          <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
            This page opens from the ShipLean app. Start signing in there to get here.
          </p>
        </Card>
      );
    }
    if (outcome?.kind === 'signedIn') {
      return (
        <Card heading="Sign in the app" testId={ids.done}>
          <Empty title={`Signed in on ${query.device}`}>
            <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
              Go back to the ShipLean app. If it didn't open, open it yourself and tap Sign in
              again.
            </p>
            <a
              data-testid={ids.openApp}
              href={outcome.url}
              className={`btn-ghost mt-2 ${fullWidth} no-underline`}
            >
              Open the app
            </a>
          </Empty>
        </Card>
      );
    }
    if (outcome?.kind === 'cancelled') {
      return (
        <Card heading="Sign in the app" testId={ids.cancelled}>
          <Empty title="Nothing was signed in">
            <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
              You can close this page.
            </p>
          </Empty>
        </Card>
      );
    }
    // No card until both reads answer: in proxy mode the Sign in button must never flash.
    if (me.data === undefined || instance.isPending) return null;

    const heading = `Sign in to ShipLean on ${query.device}?`;
    const facts = (
      <Facts me={me.data} device={query.device} instanceName={instance.data?.name} host={host} />
    );
    if (!instance.isError && instance.data?.signIn === 'proxy') {
      return (
        <Card
          heading={heading}
          subtitle={sub}
          footer={
            <>
              <button
                type="button"
                data-testid={ids.backToApp}
                className={`btn-signal ${fullWidth}`}
                onClick={() => {
                  openApp(appRedirect({ error: 'unsupported', state: query.state }));
                }}
              >
                Back to the app
              </button>
              <Note>Your admin can tell you more.</Note>
            </>
          }
        >
          {facts}
          <Banner tone="info" testId={ids.proxyMessage}>
            The ShipLean app can't sign in to {host} yet: this server signs you in through your
            company's sign-in page. Use ShipLean in your phone's browser instead.
          </Banner>
        </Card>
      );
    }
    return (
      <Card
        heading={heading}
        subtitle={sub}
        footer={
          <div className="grid gap-2">
            <button
              type="button"
              data-testid={ids.signIn}
              className={`btn-signal ${fullWidth}`}
              disabled={post.isPending}
              onClick={() => {
                post.mutate(query);
              }}
            >
              {post.isPending ? 'Signing in…' : 'Sign in'}
            </button>
            <button
              type="button"
              data-testid={ids.cancel}
              className={`btn-ghost ${fullWidth}`}
              disabled={post.isPending}
              onClick={() => {
                setOutcome({ kind: 'cancelled' });
                openApp(appRedirect({ error: 'cancelled', state: query.state }));
              }}
            >
              Cancel
            </button>
          </div>
        }
      >
        {facts}
        <div data-testid={ids.does} className="grid gap-2 text-[length:var(--text-sm)]">
          <p className="m-0 text-[length:var(--text-xs)] font-semibold tracking-wider text-ink-muted uppercase">
            What this does
          </p>
          <ul className="m-0 grid list-disc gap-2 pl-5">
            <li>The ShipLean app on that phone can see and change everything you can here.</li>
            <li>It stays signed in until you sign it out, or until it goes 90 days unused.</li>
            <li>Sign it out any time from Your profile › Signed-in devices.</li>
          </ul>
        </div>
        <WarnCallout testId={ids.warn}>
          Only sign in if you just tapped <b>Sign in</b> in the ShipLean app. If you didn't, tap
          Cancel.
        </WarnCallout>
        {post.isError ? (
          <Banner tone="danger" testId={ids.failure}>
            {unreachable}
          </Banner>
        ) : null}
      </Card>
    );
  }

  return (
    <div data-testid={ids.page} className="flex min-h-dvh flex-col">
      <LoginTopBar />
      <main className="flex flex-1 flex-col items-center px-4 pt-4 pb-5 min-[600px]:pt-[clamp(24px,10vh,120px)]">
        {body()}
      </main>
    </div>
  );
}
