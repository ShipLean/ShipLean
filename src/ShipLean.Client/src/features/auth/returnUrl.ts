// Where to go after signing in: the ?returnUrl= value when it is a local path, else the root. Local means one leading
// "/" (not "//", and not "/\", which browsers read as "//"), no whitespace or control characters (raw or
// percent-decoded in the path: browsers strip a tab, which turns "/<TAB>/host" into "//host"), the same origin once resolved, and
// not the sign-in page itself. The checks run on the resolved URL too, because resolving removes dot segments:
// "/.//host" resolves to "//host", which a navigation would read as another origin.
import { isLoginPath } from '../../loginPath';

const BASE = 'http://shiplean.invalid';

function unsafe(text: string): boolean {
  return Array.from(text).some((char) => {
    const code = char.charCodeAt(0);
    return code <= 0x1f || code === 0x7f || /\s/.test(char);
  });
}

function isLocalPath(path: string): boolean {
  return /^\/(?![/\\])/.test(path) && !unsafe(path);
}

export function localReturnUrl(value: string | null): string {
  if (value === null || !isLocalPath(value)) return '/';
  try {
    const url = new URL(value, BASE);
    // Decoded, only the path is checked: a query may carry an encoded space (scope=a%20b) that a path may not.
    if (unsafe(decodeURIComponent(url.pathname))) return '/';
    if (url.origin !== BASE || isLoginPath(url.pathname) || !isLocalPath(url.pathname)) return '/';
    const resolved = url.pathname + url.search + url.hash;
    return isLocalPath(resolved) ? resolved : '/';
  } catch {
    return '/';
  }
}

// A local path the server answers and the SPA does not route: the OAuth authorize endpoint, which sends the browser on to
// the consent page. Reaching it takes a full page load, because a router navigation would never leave the SPA.
export function isServerPath(localUrl: string): boolean {
  return localUrl.toLowerCase().startsWith('/oauth/');
}
