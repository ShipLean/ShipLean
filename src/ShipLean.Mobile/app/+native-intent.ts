// The browser handoff ends at shiplean://signed-in?…, which is not a route. On iOS the auth session catches the
// redirect itself. On Android it also arrives as an ordinary deep link: openAuthSessionAsync reads it with its own
// Linking listener, and expo-router would otherwise route it and show "Unmatched route". So expo-router ignores it
// while the app runs (a falsy return keeps the current screen, per the NativeIntent docs), and a cold start from it
// (the app was killed while the browser was open) opens Sign in, whose code is lost: the user signs in again.
import { APP_REDIRECT } from '@shiplean/core/handoff';
import type { NativeIntent } from 'expo-router';
import { links } from '../src/links';

function isAppRedirect(path: string): boolean {
  if (!path.startsWith(APP_REDIRECT)) return false;
  const next = path.charAt(APP_REDIRECT.length);
  return next === '' || next === '?' || next === '#' || next === '/';
}

export const redirectSystemPath: NativeIntent['redirectSystemPath'] = ({ path, initial }) => {
  if (!isAppRedirect(path)) return path;
  return initial ? links.signIn().pathname : null;
};
