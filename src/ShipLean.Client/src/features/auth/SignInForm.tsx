import { useMutation } from '@tanstack/react-query';
import { useRef, useState, type FormEvent, type ReactNode } from 'react';
import { ApiRequestError } from '@shiplean/core/api/client';
import { useTouchShell } from '../../touch/useTouchShell';
import type { MeDto } from '@shiplean/core/api/me';
import { login } from './authApi';
import { Banner } from './Banner';
import { FormField } from './FormField';
import { GoogleSignInLink } from './GoogleSignInLink';
import { authTestIds } from './testIds';

// The failure sentence is the server's, the same one whatever the reason, so the page never says which emails exist.
const unreachable = "Couldn't reach ShipLean. Check your connection and try again.";

export function SignInForm({
  instanceName,
  note,
  appInfo = null,
  onSignedIn,
  googleHref,
  forApp = false,
}: {
  instanceName: string | undefined;
  note: ReactNode;
  /** The line saying the sign-in is for the phone app; it follows the state banner and stays after a failed attempt. */
  appInfo?: ReactNode;
  onSignedIn: (me: MeDto, password: string) => void;
  /** Where the Google button goes; null hides it. */
  googleHref: string | null;
  /** The sign-in is for the phone app (returnUrl is its confirm page): the subtitle says so. */
  forApp?: boolean;
}) {
  const { touch } = useTouchShell();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const emailField = useRef<HTMLInputElement>(null);
  const passwordField = useRef<HTMLInputElement>(null);
  const attempt = useMutation({
    mutationFn: () => login(email.trim(), password),
    onSuccess: (me) => onSignedIn(me, password),
    onError: () => {
      setPassword('');
      passwordField.current?.focus();
    },
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (attempt.isPending) return;
    if (email.trim() === '') return emailField.current?.focus();
    if (password === '') return passwordField.current?.focus();
    attempt.mutate();
  };

  const failure =
    attempt.error instanceof ApiRequestError
      ? attempt.error.message
      : attempt.isError
        ? unreachable
        : null;

  const submitButton = (
    <button
      type="submit"
      data-testid={authTestIds.signInSubmit}
      className="btn-signal touch:h-10 touch:w-full"
      disabled={attempt.isPending}
    >
      Sign in
    </button>
  );
  const google = googleHref ? <GoogleSignInLink href={googleHref} /> : null;
  // On a phone Google comes first, then the email fields; the DOM order is the visual order.
  const orWithEmail =
    touch && google ? (
      <div
        data-testid={authTestIds.orWithEmail}
        className="text-center text-[length:var(--text-sm)] text-ink-muted"
      >
        or with your email
      </div>
    ) : null;

  return (
    <form
      data-testid={authTestIds.signInForm}
      onSubmit={submit}
      noValidate
      className="flex w-[min(380px,100%)] flex-col rounded-frame border border-edge bg-panel shadow-panel touch:w-[min(440px,100%)]"
    >
      <h1 className="m-0 border-b border-edge px-4 py-3 text-[length:var(--text-md)] font-semibold touch:text-[length:var(--text-lg)]">
        Sign in
        {instanceName || forApp ? (
          <small
            data-testid={authTestIds.subtitle}
            className="mt-0.5 block text-[length:var(--text-xs)] font-normal text-ink-muted"
          >
            {instanceName ? `to ${instanceName}` : ''}
            {instanceName && forApp ? ', ' : ''}
            {forApp ? 'for the ShipLean app' : ''}
          </small>
        ) : null}
      </h1>
      <div className="grid gap-3 p-4">
        {failure ? (
          <Banner tone="danger" testId={authTestIds.failure}>
            {failure}
          </Banner>
        ) : (
          note
        )}
        {appInfo}
        {touch ? google : null}
        {orWithEmail}
        <div className="grid grid-cols-[96px_minmax(0,1fr)] items-center gap-3 touch:grid-cols-1">
          <FormField
            id="sign-in-email"
            label="Email"
            type="email"
            value={email}
            onChange={setEmail}
            autoComplete="username"
            autoFocus={!touch}
            inputRef={emailField}
            testId={authTestIds.email}
            enterKeyHint="next"
          />
          <FormField
            id="sign-in-password"
            label="Password"
            type="password"
            value={password}
            onChange={setPassword}
            autoComplete="current-password"
            inputRef={passwordField}
            testId={authTestIds.password}
            revealTestId={authTestIds.revealPassword}
            enterKeyHint="go"
          />
        </div>
        {touch ? submitButton : google}
      </div>
      <footer className="flex items-center gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3">
        <span className="flex-1 text-[length:var(--text-xs)] text-ink-muted">
          No account, or forgot your password? Ask your ShipLean admin.
        </span>
        {touch ? null : submitButton}
      </footer>
    </form>
  );
}
