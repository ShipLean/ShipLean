import { renderRouter } from 'expo-router/testing-library';
import { queryClient } from '../queryClient';
import { session } from '../session/session';
import type { Scheme } from '../theme/themes';
import { TEST_ORIGIN, TEST_SECRET, aMe } from './builders';
import { setScheme } from './colorScheme';
import { fakeApi, type FakeApi } from './fakeApi';
import { signedInApi } from './signedInApi';

export interface RenderAppOptions {
  // Where the app opens: a route path ('/account') or a deep link ('shiplean://signed-in?…').
  url?: string;
  api?: FakeApi;
  scheme?: Scheme;
  // false: signed out, with the server (https://shiplean.test) and email kept from an earlier sign-in.
  signedIn?: boolean;
}

// Resolved from the working directory, which is the package's when the specs run (pnpm run test:screens).
const APP_DIR = './app';

// The real app/ (layout, gate, routes) through expo-router's testing library, signed in to https://shiplean.test as
// aMe() unless signedIn is false. The session is set through its own API, as a sign-in would leave it.
export async function renderApp({
  url = '/',
  api = fakeApi(signedInApi()),
  scheme = 'light',
  signedIn = true,
}: RenderAppOptions = {}) {
  setScheme(scheme);
  api.install();
  queryClient.clear();
  session.abandon();
  session.beginSignIn(TEST_ORIGIN);
  session.setPendingSecret(TEST_SECRET);
  session.commit(aMe().email);
  if (!signedIn) session.clearSecret();
  const result = await renderRouter(APP_DIR, { initialUrl: url });
  return Object.assign(result, { api });
}
