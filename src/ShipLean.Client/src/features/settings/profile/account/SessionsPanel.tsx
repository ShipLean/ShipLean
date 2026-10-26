import { useMutation, useQueryClient } from '@tanstack/react-query';
import { showToast } from '../../../../components/toastStore';
import { useSignInMode } from '../../../auth/useSignInMode';
import { signOut } from '../../../shell/shellApi';
import { resetViews, signOutOthers as signOutOthersRequest } from '../../settingsApi';
import { settingsTestIds } from '../../testIds';
import { errorText } from './errorText';
import { PanelFrame } from './PanelFrame';
import { SessionAction } from './SessionAction';

export function SessionsPanel() {
  const queryClient = useQueryClient();
  const { proxy, signOutUrl } = useSignInMode();
  const signOutOthers = useMutation({
    mutationFn: signOutOthersRequest,
    onSuccess: () =>
      showToast('Signed out everywhere else and disconnected your connected agents', {
        tone: 'ok',
      }),
  });
  const reset = useMutation({
    mutationFn: resetViews,
    onSuccess: async () => {
      // Saved views are 'view:<screen>' preferences; every preference query drops its cached copy.
      await queryClient.invalidateQueries({ queryKey: ['preferences'] });
      showToast('Saved views reset. Boards and backlogs open with their defaults', { tone: 'ok' });
    },
  });
  const leave = useMutation({
    mutationFn: signOut,
    // A full load, so nothing of this session stays in the page.
    onSuccess: () => window.location.assign(signOutUrl ?? '/login?state=signed-out'),
  });
  const failure = signOutOthers.error ?? reset.error ?? leave.error;
  return (
    <PanelFrame title="Sessions and saved views" testId={settingsTestIds.sessionsPanel}>
      <SessionAction
        title="Sign out everywhere else"
        text="Ends every other session, such as a browser you forgot to sign out of, and disconnects your connected agents. This one stays signed in."
        label="Sign out others"
        testId={settingsTestIds.signOutOthers}
        pending={signOutOthers.isPending}
        onClick={() => {
          reset.reset();
          signOutOthers.mutate();
        }}
      />
      <SessionAction
        title="Reset saved views"
        text="Forgets the columns, lanes, toggles and last filters ShipLean remembers for you on each board and backlog."
        label="Reset"
        testId={settingsTestIds.resetViews}
        pending={reset.isPending}
        onClick={() => {
          signOutOthers.reset();
          reset.mutate();
        }}
      />
      {proxy && !signOutUrl ? null : (
        <SessionAction
          title="Sign out"
          text="Of this browser."
          label="Sign out"
          testId={settingsTestIds.signOutBrowser}
          pending={leave.isPending}
          onClick={() => leave.mutate()}
        />
      )}
      {failure === null ? null : (
        <p
          data-testid={settingsTestIds.sessionsError}
          role="alert"
          className="px-3 py-2 text-[length:var(--text-xs)] text-alert"
        >
          {errorText(failure, 'That did not work. Try again.')}
        </p>
      )}
    </PanelFrame>
  );
}
