import { settingsTestIds } from '../testIds';
import { Panel } from '../draft/Panel';

const facts: [string, string][] = [
  ['How people sign in', 'Email and password. No sign-up: an admin adds each person on Users.'],
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
  return (
    <Panel title="Sign-in" testId={settingsTestIds.instanceSignInFacts}>
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Fixed in v1, shown so admins can answer questions.
      </p>
      <dl className="m-0 grid gap-x-4 gap-y-2 text-[length:var(--text-sm)] sm:grid-cols-[12rem_1fr]">
        {facts.map(([term, detail]) => (
          <div key={term} className="contents">
            <dt className="font-medium text-ink">{term}</dt>
            <dd className="m-0 text-ink-muted">{detail}</dd>
          </div>
        ))}
      </dl>
    </Panel>
  );
}
