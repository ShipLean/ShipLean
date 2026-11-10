import type { ReactNode } from 'react';
import type { MeDto } from '@shiplean/core/api/me';
import { Avatar } from '../../components/Avatar';
import { mobileSignInTestIds as ids } from './testIds';

function Row({ label, children, testId }: { label: string; children: ReactNode; testId: string }) {
  return (
    <>
      <dt className="leading-7 text-ink-muted">{label}</dt>
      <dd
        data-testid={testId}
        className="m-0 flex min-w-0 items-start gap-2 [overflow-wrap:anywhere]"
      >
        {children}
      </dd>
    </>
  );
}

const tile = 'inline-grid size-7 flex-none place-items-center rounded-md bg-sunken text-ink-muted';

// As, On and To: who the phone would be signed in as, what it calls itself, and where. The device name is text: it
// is whatever the app sent, and is never trusted.
export function Facts({
  me,
  device,
  instanceName,
  host,
}: {
  me: MeDto;
  device: string;
  instanceName: string | undefined;
  host: string;
}) {
  return (
    <dl className="m-0 grid grid-cols-[auto_minmax(0,1fr)] items-start gap-x-3 gap-y-2.5 text-[length:var(--text-sm)]">
      <Row label="As" testId={ids.as}>
        <Avatar {...me} size="lg" />
        <span>
          <b className="font-semibold">{me.name}</b>
          <span className="block text-ink-muted">{me.email}</span>
        </span>
      </Row>
      <Row label="On" testId={ids.device}>
        <span className={tile} aria-hidden="true">
          <svg
            viewBox="0 0 16 16"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            className="size-4"
          >
            <rect x="4" y="1.5" width="8" height="13" rx="1.8" />
            <path d="M7 12.2h2" />
          </svg>
        </span>
        <span>
          <b className="font-semibold">{device}</b>
          <span className="block text-ink-muted">The name the app sent</span>
        </span>
      </Row>
      <Row label="To" testId={ids.host}>
        <span className={tile} aria-hidden="true">
          <i className="block size-3 rounded-[3px] bg-signal" />
        </span>
        <span>
          <b className="font-semibold">{instanceName ?? host}</b>
          <span className="block text-ink-muted">{host}</span>
        </span>
      </Row>
    </dl>
  );
}
