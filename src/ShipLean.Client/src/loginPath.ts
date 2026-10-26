// Whether a pathname is the sign-in page. The server serves it case-insensitively and the router matches it with
// or without trailing slashes, so "/login", "/LOGIN", "/login/" and "/login//" are all the sign-in page.
export const LOGIN_PATH = '/login';

export function isLoginPath(pathname: string): boolean {
  return /^\/login\/*$/i.test(pathname);
}
