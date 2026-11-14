import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTouchShell } from '../../../../touch/useTouchShell';
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
  // At 900px and below the form starts closed, behind Change password; above it the form is always there.
  const { touch } = useTouchShell();
  const [open, setOpen] = useState(false);
  const collapsed = touch && !open;
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
        'Password changed. Your other sessions and phones are signed out and your connected agents disconnected',
        { tone: 'ok' },
      );
      setOpen(false);
      change.reset();
    },
    onError: (error) => {
      setCurrent('');
      setRefusal(errorText(error, 'The password could not be changed.'));
      change.reset();
    },
  });
  const cancel = () => {
    setCurrent('');
    setNext('');
    setConfirm('');
    setRefusal(null);
    setOpen(false);
  };
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
      className="field w-70 max-w-full touch:h-9 touch:w-full"
      value={value}
      autoComplete={auto}
      onChange={(e) => edit(set)(e.target.value)}
    />
  );
  return (
    <PanelFrame
      title="Password"
      meta="Changing it signs out your other sessions and your phones, and disconnects your connected agents"
      testId={settingsTestIds.passwordPanel}
      rule={!collapsed}
      action={
        collapsed ? (
          <button
            type="button"
            data-testid={settingsTestIds.passwordOpen}
            className="btn-ghost h-9 touch:order-2 touch:ml-auto"
            onClick={() => setOpen(true)}
          >
            Change password
          </button>
        ) : null
      }
    >
      {collapsed ? null : (
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
          <div className="grid grid-cols-[130px_minmax(0,1fr)] touch:grid-cols-1">
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
          <div className="flex items-center gap-2 rounded-b-panel border-t border-edge bg-canvas px-3 py-2 text-[length:var(--text-xs)] text-ink-muted touch:flex-wrap touch:text-[length:var(--text-sm)]">
            {refusal !== null ? (
              <span
                data-testid={settingsTestIds.passwordError}
                role="alert"
                className="text-alert touch:order-3 touch:basis-full"
              >
                {refusal}
              </span>
            ) : (
              <span className="touch:order-3 touch:basis-full">
                Forgot it? Another instance admin can reset it.
              </span>
            )}
            <span className="flex-1 touch:hidden" />
            {touch ? (
              <button
                type="button"
                data-testid={settingsTestIds.passwordCancel}
                className="btn-ghost h-9 flex-1 justify-center"
                onClick={cancel}
              >
                Cancel
              </button>
            ) : null}
            <button
              type="submit"
              data-testid={settingsTestIds.passwordSubmit}
              className="btn-signal touch:h-9 disabled:opacity-50 touch:flex-1 touch:justify-center"
              disabled={!passwordReady(checks) || change.isPending}
            >
              Change password
            </button>
          </div>
        </form>
      )}
    </PanelFrame>
  );
}
