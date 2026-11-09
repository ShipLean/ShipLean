// The web host of @shiplean/core's platform seam: the browser's storage and connectivity, the page's own origin and
// cookie, and a lost session sends the page to the sign-in page.
import { isLoginPath, LOGIN_PATH } from '@shiplean/core/loginPath';
import type { KeyValueStore, Platform, UnauthorizedReason } from '@shiplean/core/platform';

// Reads the storage global on each call: a browser that refuses storage throws on the access itself, and the
// KeyValueStore contract lets that throw reach the caller.
function browserStore(store: () => Storage): KeyValueStore {
  return {
    getItem: (k) => store().getItem(k),
    setItem: (k, v) => store().setItem(k, v),
    removeItem: (k) => store().removeItem(k),
  };
}

// The sign-in page this page load is already leaving for. When a session ends, several requests fail together: the
// first answers session_ended and its response deletes the cookie, so the ones sent after it answer a plain 401. Each
// later assign() would replace the navigation already under way (location.pathname only changes once it commits), so
// the first redirect stands, except that a named state replaces a plain one.
let leavingFor: 'plain' | 'state' | undefined;

// A 401 means the session is gone. Not on the sign-in page itself, where it is a wrong password.
function sendToLogin() {
  const { pathname, search } = window.location;
  if (isLoginPath(pathname) || leavingFor !== undefined) return;
  leavingFor = 'plain';
  window.location.assign(`${LOGIN_PATH}?returnUrl=${encodeURIComponent(pathname + search)}`);
}

// The two sign-in states the server names with a code: the session ended (password or stamp changed elsewhere, or the
// account was disabled), or the one-time password must be replaced first. Both open the sign-in page in that state,
// with the path to come back to.
function sendToLoginState(state: 'ended' | 'change') {
  const { pathname, search } = window.location;
  if (isLoginPath(pathname) || leavingFor === 'state') return;
  leavingFor = 'state';
  window.location.assign(
    `${LOGIN_PATH}?state=${state}&returnUrl=${encodeURIComponent(pathname + search)}`,
  );
}

const persistent = browserStore(() => window.localStorage);
const session = browserStore(() => window.sessionStorage);

export const webPlatform: Platform = {
  persistentStore: () => persistent,
  sessionStore: () => session,
  isOffline: () => !navigator.onLine,
  onOnline: (listener) => {
    window.addEventListener('online', listener);
    return () => window.removeEventListener('online', listener);
  },
  onUnauthorized: (reason: UnauthorizedReason) => {
    if (reason === 'session_ended') sendToLoginState('ended');
    else if (reason === 'password_change_required') sendToLoginState('change');
    else sendToLogin();
  },
  apiOrigin: () => '',
  authHeaders: () => ({}),
  credentials: () => 'same-origin',
};
