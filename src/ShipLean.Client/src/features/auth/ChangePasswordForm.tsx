import { useMutation } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import type { MeDto } from '../../api/me';
import { ApiRequestError } from '../../api/client';
import { changePassword } from './authApi';
import { Banner } from './Banner';
import { FormField } from './FormField';
import { SignOutInstead } from './SignOutInstead';
import { authTestIds } from './testIds';

export const MIN_PASSWORD_LENGTH = 10;

// Choose your own password: shown after signing in with a one-time password, before anything else opens.
export function ChangePasswordForm({
  email,
  oneTimePassword,
  onChanged,
}: {
  email: string;
  oneTimePassword: string;
  onChanged: (me: MeDto) => void;
}) {
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const save = useMutation({
    mutationFn: () => changePassword(oneTimePassword, next),
    onSuccess: onChanged,
  });

  const rules: [boolean, string][] = [
    [next.length >= MIN_PASSWORD_LENGTH, `At least ${MIN_PASSWORD_LENGTH} characters`],
    [next.length > 0 && next !== oneTimePassword, 'Not the one-time password'],
    [next.length > 0 && next === confirm, 'Both match'],
  ];
  const ready = rules.every(([met]) => met);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (ready && !save.isPending) save.mutate();
  };

  return (
    <form
      data-testid={authTestIds.changeForm}
      onSubmit={submit}
      noValidate
      className="flex w-[min(460px,100%)] flex-col rounded-frame border border-edge bg-panel shadow-panel"
    >
      <h1 className="m-0 border-b border-edge px-4 py-3 text-[length:var(--text-md)] font-semibold">
        Choose your own password
        <small className="mt-0.5 block text-[length:var(--text-xs)] font-normal text-ink-muted">
          Signed in as {email}
        </small>
      </h1>
      <div className="grid gap-3 p-4">
        <Banner tone="info" testId={authTestIds.note}>
          You signed in with a one-time password from an admin. Pick one only you know before you
          carry on.
        </Banner>
        {save.error ? (
          <Banner tone="danger" testId={authTestIds.changeFailure}>
            {save.error instanceof ApiRequestError
              ? save.error.message
              : "Couldn't reach ShipLean. Check your connection and try again."}
          </Banner>
        ) : null}
        <div className="grid grid-cols-[96px_minmax(0,1fr)] items-center gap-3">
          <FormField
            id="new-password"
            label="New password"
            type="password"
            value={next}
            onChange={setNext}
            autoComplete="new-password"
            autoFocus
            testId={authTestIds.newPassword}
            revealTestId={authTestIds.revealPassword}
          />
          <FormField
            id="confirm-password"
            label="Confirm"
            type="password"
            value={confirm}
            onChange={setConfirm}
            autoComplete="new-password"
            testId={authTestIds.confirmPassword}
          />
          <ul
            data-testid={authTestIds.passwordChecks}
            className="col-start-2 m-0 grid list-none gap-0.5 p-0 text-[length:var(--text-sm)]"
          >
            {rules.map(([met, text]) => (
              <li key={text} data-met={met} className={met ? '' : 'text-ink-muted'}>
                <span
                  aria-hidden="true"
                  className="inline-block w-4"
                  style={met ? { color: 'var(--color-ok)', fontWeight: 700 } : undefined}
                >
                  {met ? '✓' : '○'}
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>
      </div>
      <footer className="flex items-center gap-2 rounded-b-frame border-t border-edge bg-canvas px-4 py-3">
        <span className="flex-1">
          <SignOutInstead
            label="Sign out instead"
            className="text-[length:var(--text-xs)] text-signal underline"
          />
        </span>
        <button
          type="submit"
          data-testid={authTestIds.changeSubmit}
          className="btn-signal"
          disabled={!ready || save.isPending}
        >
          Save and continue
        </button>
      </footer>
    </form>
  );
}
