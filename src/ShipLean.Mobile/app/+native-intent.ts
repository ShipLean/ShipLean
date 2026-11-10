// The browser handoff ends at shiplean://signed-in?…, which is not a route. On iOS the auth session catches the
// redirect itself. On Android it also arrives as an ordinary deep link: openAuthSessionAsync reads it with its own
// Linking listener, and expo-router would otherwise route it and show "Unmatched route". So expo-router ignores it
// while the app runs (a falsy return keeps the current screen, per the NativeIntent docs), and a cold start from it
// (the app was killed while the browser was open) opens Sign in, whose code is lost: the user signs in again.
import { APP_REDIRECT } from '@shiplean/core/handoff';
import { links } from '../src/links';

// The redirect as the device sends it (shiplean://signed-in?…) and as expo-router reads it (/signed-in?…: a custom
// scheme's host is the first path segment).
const REDIRECT_PATH = '/' + APP_REDIRECT.slice(APP_REDIRECT.indexOf('://') + 3);

function startsWithRoute(path: string, route: string): boolean {
  if (!path.startsWith(route)) return false;
  const next = path.charAt(route.length);
  return next === '' || next === '?' || next === '#' || next === '/';
}

function isAppRedirect(path: string): boolean {
  return startsWithRoute(path, APP_REDIRECT) || startsWithRoute(path, REDIRECT_PATH);
}

// expo-router's NativeIntent.redirectSystemPath.
export function redirectSystemPath({
  path,
  initial,
}: {
  path: string;
  initial: boolean;
}): string | null {
  if (!isAppRedirect(path)) return path;
  return initial ? links.signIn().pathname : null;
}
