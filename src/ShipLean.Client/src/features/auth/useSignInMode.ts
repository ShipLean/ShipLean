import { useQuery } from '@tanstack/react-query';
import { fetchInstance, instanceKey } from './authApi';

export interface SignInMode {
  proxy: boolean;
  signOutUrl: string | null;
}

const passwordMode: SignInMode = { proxy: false, signOutUrl: null };

/** Which sign-in screens to show, from the shared instance query. Password mode while it loads or once a read fails. */
export function useSignInMode(): SignInMode {
  const instance = useQuery({ queryKey: instanceKey, queryFn: fetchInstance, retry: false });
  // An error wins over an earlier answer: a failed refetch keeps the old data, but reads as password mode.
  if (instance.isError || instance.data?.signIn !== 'proxy') {
    return passwordMode;
  }
  return { proxy: true, signOutUrl: instance.data.signOutUrl };
}
