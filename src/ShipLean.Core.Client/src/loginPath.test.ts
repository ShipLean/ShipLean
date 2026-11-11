import { describe, expect, test } from 'vitest';
import { isLoginPath } from './loginPath';

describe('isLoginPath', () => {
  test.each([['/login'], ['/LOGIN'], ['/Login'], ['/login/'], ['/login//']])(
    'isLoginPath_WhenSignInPage_ShouldBeTrue (%s)',
    (pathname) => {
      expect(isLoginPath(pathname)).toBe(true);
    },
  );

  test.each([['/'], ['/login-help'], ['/login/x'], ['/p/login'], ['/loginx'], ['']])(
    'isLoginPath_WhenAnotherPage_ShouldBeFalse (%s)',
    (pathname) => {
      expect(isLoginPath(pathname)).toBe(false);
    },
  );
});
