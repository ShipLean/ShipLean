import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { showToast } from '../../../../components/toastStore';
import { fetchMe, meKey } from '../../../../queries/shared';
import { changePassword } from '../../settingsApi';
import { settingsTestIds } from '../../testIds';
import { errorText } from './errorText';
import { FormRow } from '../identity/FormRow';
import { PanelFrame } from './PanelFrame';
import { passwordChecks, passwordReady } from './passwordRules';

export function PasswordPanel() {
  const queryClient = useQueryClient();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [refusal, setRefusal] = useState<string | null>(null);
  const email = useQuery({ queryKey: meKey, queryFn: fetchMe }).data?.email ?? '';
  const checks = passwordChecks(current, next, confirm);
  // The mutation function closes over both passwords, so it must not outlive the attempt: gcTime 0 drops it
  // with its last observer, and reset() releases the observer as soon as the attempt is answered. The
  // refusal is kept as text for that reason instead of in the mutation's error.
  const change = useMutation({
    gcTime: 0,
    mutationFn: () => changePassword({ current, new: next }),
    onSuccess: (me) => {
      queryClient.setQueryData(meKey, me);
      setCurrent('');
      setNext('');
      setConfirm('');
      showToast(
        'Password changed. Your other sessions are signed out and your connected agents disconnected',
        { tone: 'ok' },
      );
      change.reset();
    },
    onError: (error) => {
      setCurrent('');
      setRefusal(errorText(error, 'The password could not be changed.'));
      change.reset();
    },
  });
  const submit = () => {
    if (passwordReady(checks) && !change.isPending) change.mutate();
  };
  const edit = (set: (value: string) => void) => (value: string) => {
    setRefusal(null);
    set(value);
  };
  const field = (
    id: string,
    testId: string,
    value: string,
    set: (v: string) => void,
    auto: string,
  ) => (
    <input
      id={id}
      data-testid={testId}
      type="password"
      className="field w-70 max-w-full"
      value={value}
      autoComplete={auto}
      onChange={(e) => edit(set)(e.target.value)}
    />
  );
  return (
    <PanelFrame
      title="Password"
      meta="Changing it signs out your other sessions and disconnects your connected agents"
      testId={settingsTestIds.passwordPanel}
    >
      <form
        data-testid={settingsTestIds.passwordForm}
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
      >
        <input
          data-testid={settingsTestIds.passwordUsername}
          type="text"
          className="sr-only"
          autoComplete="username"
          value={email}
          readOnly
          tabIndex={-1}
          aria-hidden="true"
        />
        <div className="grid grid-cols-[130px_minmax(0,1fr)] max-sm:grid-cols-1">
          <FormRow label="Current" htmlFor="settings-password-current">
            {field(
              'settings-password-current',
              settingsTestIds.passwordCurrent,
              current,
              setCurrent,
              'current-password',
            )}
          </FormRow>
          <FormRow label="New" htmlFor="settings-password-new">
            {field(
              'settings-password-new',
              settingsTestIds.passwordNew,
              next,
              setNext,
              'new-password',
            )}
          </FormRow>
          <FormRow label="Confirm new" htmlFor="settings-password-confirm">
            {field(
              'settings-password-confirm',
              settingsTestIds.passwordConfirm,
              confirm,
              setConfirm,
              'new-password',
            )}
            <ul className="grid gap-0.5 text-[length:var(--text-sm)]">
              {checks.map((check) => (
                <li
                  key={check.label}
                  data-testid={settingsTestIds.passwordCheck}
                  data-met={check.met}
                  className={check.met ? 'text-ink' : 'text-ink-muted'}
                >
                  {check.met ? '✓' : '○'} {check.label}
                </li>
              ))}
            </ul>
          </FormRow>
        </div>
        <div className="flex items-center gap-2 rounded-b-panel border-t border-edge bg-canvas px-3 py-2 text-[length:var(--text-xs)] text-ink-muted">
          {refusal !== null ? (
            <span data-testid={settingsTestIds.passwordError} role="alert" className="text-alert">
              {refusal}
            </span>
          ) : (
            'Forgot it? Another instance admin can reset it.'
          )}
          <span className="flex-1" />
          <button
            type="submit"
            data-testid={settingsTestIds.passwordSubmit}
            className="btn-signal disabled:opacity-50"
            disabled={!passwordReady(checks) || change.isPending}
          >
            Change password
          </button>
        </div>
      </form>
    </PanelFrame>
  );
}
