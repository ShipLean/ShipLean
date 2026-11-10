import { isLoginPath } from '@shiplean/core/loginPath';
import { expect, test } from 'vitest';

// The app reaches the shared core through the workspace link, not a relative path.
test('coreImport_WhenResolvedFromTheApp_ShouldRunTheSharedCode', () => {
  expect(isLoginPath('/login')).toBe(true);
});
