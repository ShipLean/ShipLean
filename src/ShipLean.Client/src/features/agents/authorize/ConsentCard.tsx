import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import {
  decideConsent,
  getConsentRequest,
  type ConsentRequestDto,
  type ConsentResult,
} from '../../../api/agents';
import { ApiRequestError } from '../../../api/client';
import { Chip } from '../../../components/Chip';
import { Banner } from '../../auth/Banner';
import { WarnCallout } from './WarnCallout';
import { agentsTestIds as ids } from '../testIds';
import { returnAddress } from './returnAddress';
import { agentScopes, readScope, scopePhrase } from './scopes';

export const consentKey = (requestId: string) => ['agents', 'consent', requestId] as const;

const unusableAddress =
  "ShipLean can't send you back to the agent: its return address is not a web address or an app's own address.";

// The page behind /authorize?request=<id>: who is asking, what for, and Allow or Deny. Everything about the client is
// rendered as text (React escapes it); the only place the page navigates to is the address the server answers with.
export function ConsentCard({ requestId }: { requestId: string | null }) {
  const request = useQuery({
    queryKey: consentKey(requestId ?? ''),
    queryFn: () => getConsentRequest(requestId ?? ''),
    enabled: requestId !== null,
    retry: false,
  });

  if (requestId === null) {
    return (
      <Notice>This page opens from your agent. Start the connection there to get here.</Notice>
    );
  }
  if (request.isPending) return null;
  if (request.isError) {
    return (
      <Notice>
        {request.error instanceof ApiRequestError && request.error.code === 'not_found'
          ? 'This request has expired or is not yours. Start the connection again from your agent.'
          : "Couldn't load this request. Reload the page to try again."}
      </Notice>
    );
  }
  return <ConsentForm request={request.data} />;
}

function Notice({ children }: { children: string }) {
  return (
    <section
      data-testid={ids.consentCard}
      className="panel mx-auto w-[min(460px,100%)] bg-panel p-4 text-[length:var(--text-sm)]"
    >
      <h1 className="page-title mb-2">Connect an agent</h1>
      <p data-testid={ids.consentError} className="m-0 text-ink-muted">
        {children}
      </p>
    </section>
  );
}

function ConsentForm({ request }: { request: ConsentRequestDto }) {
  // Read is always granted; the rest start as requested.
  const requested = new Set([readScope, ...request.requestedScopes]);
  const [ticked, setTicked] = useState<ReadonlySet<string>>(requested);
  const [refused, setRefused] = useState(false);

  const decide = useMutation<ConsentResult, Error, boolean>({
    mutationFn: (allow) =>
      decideConsent(request.requestId, {
        allow,
        scopes: allow ? agentScopes.map((s) => s.scope).filter((s) => ticked.has(s)) : [],
      }),
    onSuccess: (result) => {
      const address = returnAddress(result.redirectUrl);
      if (address === null) setRefused(true);
      else window.location.assign(address);
    },
  });

  // The server names a private-use redirect as scheme://host (cursor://anysphere.cursor-retrieval), a web one by its host.
  const isApp = request.redirectHost.includes('://');
  const working = decide.isPending || (decide.isSuccess && !refused);
  const toggle = (scope: string) => {
    const next = new Set(ticked);
    if (next.has(scope)) next.delete(scope);
    else next.add(scope);
    setTicked(next);
  };

  const order = (scope: string) => {
    const at = agentScopes.findIndex((known) => known.scope === scope);
    return at === -1 ? agentScopes.length : at;
  };
  const wants = [...requested]
    .sort((a, b) => order(a) - order(b))
    .map(scopePhrase)
    .join(' · ');

  const failure = refused
    ? unusableAddress
    : decide.error instanceof ApiRequestError
      ? decide.error.message
      : decide.isError
        ? "Couldn't reach ShipLean. Check your connection and try again."
        : null;

  return (
    <section
      data-testid={ids.consentCard}
      className="panel mx-auto flex w-[min(460px,100%)] flex-col bg-panel shadow-panel"
    >
      <h1 className="m-0 border-b border-edge px-4 py-3 text-[length:var(--text-md)] font-semibold">
        <span data-testid={ids.consentClient}>{request.clientName}</span>
        <small className="mt-0.5 flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] font-normal text-ink-muted">
          wants to connect to ShipLean as you
        </small>
      </h1>
      <div className="grid gap-3 p-4 text-[length:var(--text-sm)]">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-ink-muted">Host</span>
          <span data-testid={ids.consentHost} data-verified={request.verified}>
            {request.verified && request.clientHost !== null ? (
              <Chip tone="ok">{request.clientHost}</Chip>
            ) : (
              <Chip tone="warn">unverified</Chip>
            )}
          </span>
          <span className="text-ink-muted">· returns to</span>
          <b data-testid={ids.consentRedirectHost}>{request.redirectHost}</b>
          {isApp ? (
            <span data-testid={ids.consentRedirectApp} className="text-ink-muted">
              (an app on this computer)
            </span>
          ) : null}
        </div>
        {request.verified ? null : (
          <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
            Anyone can register a client under any name. Allow it only if you just started it
            yourself.
          </p>
        )}
        {request.localhostOnly ? (
          <WarnCallout testId={ids.consentLocalhostWarning}>
            This agent can only send you back to an address on your own computer (localhost). Allow
            it only if you started it there.
          </WarnCallout>
        ) : null}
        <p data-testid={ids.consentWants} className="m-0">
          Wants to: {wants}
        </p>
        <fieldset className="m-0 grid gap-1.5 border-0 p-0">
          <legend className="mb-1 text-ink-muted">Let it</legend>
          {agentScopes.map((s) => {
            const isRead = s.scope === readScope;
            const asked = requested.has(s.scope);
            return (
              <label
                key={s.scope}
                className={`flex items-center gap-2 ${asked ? '' : 'text-ink-muted'}`}
              >
                <input
                  type="checkbox"
                  data-testid={s.testId}
                  checked={ticked.has(s.scope)}
                  disabled={isRead || !asked || working}
                  onChange={() => toggle(s.scope)}
                />
                <span>
                  <b>{s.label}</b> · {s.phrase}
                  {isRead ? ' (always)' : asked ? '' : ' (not requested)'}
                </span>
              </label>
            );
          })}
        </fieldset>
        {failure ? (
          <Banner tone="danger" testId={ids.consentError}>
            {failure}
          </Banner>
        ) : null}
        {working && decide.isSuccess ? (
          <p data-testid={ids.consentLeaving} className="m-0 text-ink-muted">
            Returning to {request.redirectHost}
          </p>
        ) : null}
      </div>
      <footer className="flex items-center justify-end gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3">
        <button
          type="button"
          data-testid={ids.consentDeny}
          className="btn-ghost"
          disabled={working}
          onClick={() => decide.mutate(false)}
        >
          Deny
        </button>
        <button
          type="button"
          data-testid={ids.consentAllow}
          className="btn-signal"
          disabled={working}
          onClick={() => decide.mutate(true)}
        >
          Allow
        </button>
      </footer>
    </section>
  );
}
