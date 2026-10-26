import { useMutation } from '@tanstack/react-query';
import { useRef, useState, type FormEvent, type ReactNode } from 'react';
import { ApiRequestError } from '../../api/client';
import type { MeDto } from '../../api/me';
import { login } from './authApi';
import { Banner } from './Banner';
import { FormField } from './FormField';
import { authTestIds } from './testIds';

// The failure sentence is the server's, the same one whatever the reason, so the page never says which emails exist.
const unreachable = "Couldn't reach ShipLean. Check your connection and try again.";

export function SignInForm({
  instanceName,
  note,
  onSignedIn,
}: {
  instanceName: string | undefined;
  note: ReactNode;
  onSignedIn: (me: MeDto, password: string) => void;
}) {
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

  return (
    <form
      data-testid={authTestIds.signInForm}
      onSubmit={submit}
      noValidate
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
        {failure ? (
          <Banner tone="danger" testId={authTestIds.failure}>
            {failure}
          </Banner>
        ) : (
          note
        )}
        <div className="grid grid-cols-[96px_minmax(0,1fr)] items-center gap-3">
          <FormField
            id="sign-in-email"
            label="Email"
            type="email"
            value={email}
            onChange={setEmail}
            autoComplete="username"
            autoFocus
            inputRef={emailField}
            testId={authTestIds.email}
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
          />
        </div>
      </div>
      <footer className="flex items-center gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3">
        <span className="flex-1 text-[length:var(--text-xs)] text-ink-muted">
          No account, or forgot your password? Ask your ShipLean admin.
        </span>
        <button
          type="submit"
          data-testid={authTestIds.signInSubmit}
          className="btn-signal"
          disabled={attempt.isPending}
        >
          Sign in
        </button>
      </footer>
    </form>
  );
}
