import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import type { MeDto } from '../../api/me';
import { ToastHost } from '../../components/ToastHost';
import { showToast } from '../../components/toastStore';
import { appTestIds } from '../../testIds';
import { fetchMe, meKey, projectsKey } from '../../queries/shared';
import { fetchInstance, instanceKey } from './authApi';
import { Banner } from './Banner';
import { ChangePasswordForm } from './ChangePasswordForm';
import { LoginTopBar } from './LoginTopBar';
import { NoProjectsCard } from './NoProjectsCard';
import { isServerPath, localReturnUrl } from './returnUrl';
import { ProxySignInPage } from './ProxySignInPage';
import { SignInForm } from './SignInForm';
import { authTestIds } from './testIds';
import { useSignInMode } from './useSignInMode';

const notes: Record<string, { tone: 'ok' | 'info'; text: string }> = {
  'signed-out': { tone: 'ok', text: "You're signed out." },
  ended: {
    tone: 'info',
    text: 'You were signed out because your password was changed or reset. Sign in again to carry on.',
  },
};

const changeNote =
  'Sign in with the one-time password from your admin first. Then you can choose your own.';

// The one-time password just typed, kept in memory (never stored) until the new one is saved.
interface PendingChange {
  email: string;
  oneTimePassword: string;
}

// /login, and the states after it (?state=change | signed-out | ended | no-projects).
export function LoginPage() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [pending, setPending] = useState<PendingChange | null>(null);
  const state = params.get('state');
  const returnUrl = localReturnUrl(params.get('returnUrl'));

  const instance = useQuery({ queryKey: instanceKey, queryFn: fetchInstance, retry: false });
  const { proxy } = useSignInMode();
  const me = useQuery({
    queryKey: meKey,
    queryFn: fetchMe,
    retry: false,
    enabled: state === 'no-projects',
  });

  useEffect(() => {
    const before = document.title;
    document.title = instance.data ? `Sign in · ${instance.data.name}` : 'Sign in';
    return () => {
      document.title = before;
    };
  }, [instance.data]);

  const goToReturnUrl = () => {
    if (isServerPath(returnUrl)) window.location.assign(returnUrl);
    else navigate(returnUrl, { replace: true });
  };

  const signedIn = (user: MeDto) => {
    queryClient.setQueryData(meKey, user);
    void queryClient.invalidateQueries({ queryKey: projectsKey });
  };

  const onSignedIn = (user: MeDto, password: string) => {
    signedIn(user);
    if (user.status === 'oneTimePassword') {
      setPending({ email: user.email, oneTimePassword: password });
      const next = new URLSearchParams(params);
      next.set('state', 'change');
      setParams(next, { replace: true });
    } else {
      showToast(returnUrl === '/' ? 'Signed in.' : 'Signed in. Opening where you were going');
      goToReturnUrl();
    }
  };

  const onChanged = (user: MeDto) => {
    signedIn(user);
    setPending(null);
    showToast('Password saved. Other sessions are signed out');
    goToReturnUrl();
  };

  const note =
    state === 'change' ? (
      <Banner tone="info" testId={authTestIds.note}>
        {changeNote}
      </Banner>
    ) : state !== null && state in notes ? (
      <Banner tone={notes[state].tone} testId={authTestIds.note}>
        {notes[state].text}
      </Banner>
    ) : null;

  const card = cardFor();

  function cardFor() {
    if (state === 'change' && pending !== null) {
      return <ChangePasswordForm {...pending} onChanged={onChanged} />;
    }
    if (state === 'no-projects' && me.isPending) return null;
    if (state === 'no-projects' && me.data) {
      return <NoProjectsCard me={me.data} instanceName={instance.data?.name} />;
    }
    // No form until the instance answers: in proxy mode it must never flash. A failed read reads as password mode.
    if (instance.isPending) return null;
    if (proxy) {
      return (
        <ProxySignInPage
          state={state}
          instanceName={instance.data?.name}
          onSignedIn={goToReturnUrl}
          onTryAgain={() => window.location.assign(returnUrl)}
        />
      );
    }
    return <SignInForm instanceName={instance.data?.name} note={note} onSignedIn={onSignedIn} />;
  }

  return (
    <div data-testid={appTestIds.loginPage} className="flex min-h-dvh flex-col">
      <LoginTopBar />
      <main className="grid flex-1 place-items-center px-4 py-5">{card}</main>
      <ToastHost />
    </div>
  );
}
