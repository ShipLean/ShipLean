import { useQuery } from '@tanstack/react-query';
import { fetchInstance, instanceKey, type InstanceDto } from '../../auth/authApi';
import { settingsTestIds } from '../testIds';
import { Panel } from '../draft/Panel';

/** What the "How people sign in" row says, from the same instance answer the sign-in page reads. */
export function signInSummary(instance: Pick<InstanceDto, 'signIn' | 'googleSignIn'> | undefined) {
  if (instance?.signIn === 'proxy')
    return 'Your identity proxy signs people in, with no password. A user is created the first time the proxy sees their email.';
  if (instance?.googleSignIn)
    return 'Email and password, or Google. An admin can add people on Users, and anyone Google admits gets a user on first sign-in.';
  return 'Email and password. No sign-up: an admin adds each person on Users.';
}

const facts: [string, string][] = [
  [
    'Forgotten passwords',
    'ShipLean sends no email. An admin resets the password and hands over the one-time password.',
  ],
  [
    'Sessions',
    'Stay signed in for 14 days after you last used ShipLean. Changing or resetting a password signs out every other session.',
  ],
  [
    'Lockout',
    '10 wrong passwords in a row lock the account for 15 minutes. Admins can unlock sooner on Users.',
  ],
  [
    'First admin',
    'SHIPLEAN_ADMIN_EMAIL and SHIPLEAN_ADMIN_PASSWORD on first boot. Later admins are made on Users.',
  ],
];

/** The sign-in rules that aren't settings in v1, shown so admins can answer questions. */
export function SignInRules() {
  const instance = useQuery({ queryKey: instanceKey, queryFn: fetchInstance, retry: false });
  // Until the answer arrives the row says nothing, rather than a sentence that may be wrong; a failed read gives the password text, as the sign-in page does.
  const summary = instance.isPending
    ? ''
    : signInSummary(instance.isError ? undefined : instance.data);
  const rows: [string, string][] = [['How people sign in', summary], ...facts];
  return (
    <Panel title="Sign-in" testId={settingsTestIds.instanceSignInFacts}>
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Fixed in v1, shown so admins can answer questions.
      </p>
      <dl className="m-0 grid gap-x-4 gap-y-2 text-[length:var(--text-sm)] sm:grid-cols-[12rem_1fr]">
        {rows.map(([term, detail]) => (
          <div key={term} className="contents">
            <dt className="font-medium text-ink">{term}</dt>
            <dd className="m-0 text-ink-muted">{detail}</dd>
          </div>
        ))}
      </dl>
    </Panel>
  );
}
