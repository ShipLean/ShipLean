import { useMutation } from '@tanstack/react-query';
import { showToast } from '../../components/toastStore';
import { logout } from './authApi';
import { authTestIds } from './testIds';
import { useSignInMode } from './useSignInMode';

// "Sign out instead" on the sign-in steps that sit between signing in and the app (choosing a password, no projects).
export function SignOutInstead({ label, className }: { label: string; className: string }) {
  const { proxy, signOutUrl } = useSignInMode();
  const leave = useMutation({
    mutationFn: logout,
    // A full load, so nothing of this session stays in the page. In proxy mode the proxy ends its own session too.
    onSuccess: () =>
      window.location.assign(proxy && signOutUrl ? signOutUrl : '/login?state=signed-out'),
    onError: () => showToast('Could not sign out. Try again.', { tone: 'danger' }),
  });
  if (proxy && !signOutUrl) return null;
  return (
    <button
      type="button"
      data-testid={authTestIds.signOut}
      className={className}
      disabled={leave.isPending}
      onClick={() => leave.mutate()}
    >
      {label}
    </button>
  );
}
