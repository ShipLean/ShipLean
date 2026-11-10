import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiRequestError } from '@shiplean/core/api/client';
import { ToastHost } from '../../components/ToastHost';
import { showToast } from '../../components/toastStore';
import { meKey, projectsKey } from '../../queries/shared';
import { appTestIds } from '../../testIds';
import { signUp } from './authApi';
import { Banner } from './Banner';
import { FormField } from './FormField';
import { LoginTopBar } from './LoginTopBar';
import { authTestIds } from './testIds';

const unreachable = "Couldn't reach ShipLean. Check your connection and try again.";

// /signup: a name, an email and a password make a user and sign them in. The server answers 404 with its own sentence
// while SHIPLEAN_SIGNUP_ENABLED is off, and the page shows that sentence; nothing links here.
export function SignUpPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const nameField = useRef<HTMLInputElement>(null);
  const emailField = useRef<HTMLInputElement>(null);
  const passwordField = useRef<HTMLInputElement>(null);
  const attempt = useMutation({
    mutationFn: () => signUp(name.trim(), email.trim(), password),
    onSuccess: (me) => {
      queryClient.setQueryData(meKey, me);
      void queryClient.invalidateQueries({ queryKey: projectsKey });
      showToast('Account created. You are signed in.');
      navigate('/', { replace: true });
    },
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (attempt.isPending) return;
    if (name.trim() === '') return nameField.current?.focus();
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
    <div data-testid={appTestIds.loginPage} className="flex min-h-dvh flex-col">
      <LoginTopBar />
      <main className="grid flex-1 place-items-center px-4 py-5">
        <form
          data-testid={authTestIds.signUpForm}
          onSubmit={submit}
          noValidate
          className="flex w-[min(380px,100%)] flex-col rounded-frame border border-edge bg-panel shadow-panel"
        >
          <h1 className="m-0 border-b border-edge px-4 py-3 text-[length:var(--text-md)] font-semibold">
            Create an account
          </h1>
          <div className="grid gap-3 p-4">
            {failure ? (
              <Banner tone="danger" testId={authTestIds.failure}>
                {failure}
              </Banner>
            ) : null}
            <div className="grid grid-cols-[96px_minmax(0,1fr)] items-center gap-3">
              <FormField
                id="sign-up-name"
                label="Name"
                type="text"
                value={name}
                onChange={setName}
                autoComplete="name"
                autoFocus
                inputRef={nameField}
                testId={authTestIds.signUpName}
              />
              <FormField
                id="sign-up-email"
                label="Email"
                type="email"
                value={email}
                onChange={setEmail}
                autoComplete="username"
                inputRef={emailField}
                testId={authTestIds.email}
              />
              <FormField
                id="sign-up-password"
                label="Password"
                type="password"
                value={password}
                onChange={setPassword}
                autoComplete="new-password"
                inputRef={passwordField}
                testId={authTestIds.password}
                revealTestId={authTestIds.revealPassword}
              />
            </div>
          </div>
          <footer className="flex items-center gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3">
            <span className="flex-1 text-[length:var(--text-xs)] text-ink-muted">
              Already have an account?{' '}
              <Link data-testid={authTestIds.signInInstead} to="/login" className="underline">
                Sign in
              </Link>
            </span>
            <button
              type="submit"
              data-testid={authTestIds.signUpSubmit}
              className="btn-signal"
              disabled={attempt.isPending}
            >
              Create account
            </button>
          </footer>
        </form>
      </main>
      <ToastHost />
    </div>
  );
}
