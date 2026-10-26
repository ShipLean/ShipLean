import { describe, expect, test } from 'vitest';
import { isServerPath, localReturnUrl } from './returnUrl';

describe('localReturnUrl', () => {
  test.each([
    ['/p/SHIP/backlogs/stories?mode=tree', '/p/SHIP/backlogs/stories?mode=tree'],
    ['/profile', '/profile'],
    ['/login-help', '/login-help'],
    [
      '/oauth/authorize?scope=shiplean%3Aread%20shiplean%3Awrite&state=a%20b',
      '/oauth/authorize?scope=shiplean%3Aread%20shiplean%3Awrite&state=a%20b',
    ],
  ])('localReturnUrl_WhenLocalPath_ShouldKeepIt (%s)', (value, expected) => {
    expect(localReturnUrl(value)).toBe(expected);
  });

  test.each([
    [null],
    [''],
    ['https://example.com/'],
    ['//example.com/'],
    ['/\\example.com'],
    ['example.com'],
    ['/login'],
    ['/login?returnUrl=%2F'],
    ['/login#x'],
    ['/\t/example.com'],
    ['/ /example.com'],
    ['/%09/example.com'],
    ['/%20%2F%2Fevil'],
    ['/%20//evil.com'],
    ['/%2F%09%2Fexample.com'],
    ['/%0A/example.com'],
    ['/a%'],
    ['/./login'],
    ['/login/'],
    ['/LOGIN'],
    ['/Login/'],
    ['/login//'],
    ['/.//evil.com'],
    ['/..//evil.com'],
    ['/%2e%2e//evil.com'],
    ['/a/..\\/evil.com'],
    ['/.\\/evil.com'],
  ])('localReturnUrl_WhenNotAcceptable_ShouldBeTheRoot (%s)', (value) => {
    expect(localReturnUrl(value)).toBe('/');
  });
});

describe('isServerPath', () => {
  test.each([
    ['/oauth/authorize?client_id=x', true],
    ['/oauth/', true],
    ['/OAuth/authorize', true],
    ['/oauthx', false],
    ['/p/SHIP/backlogs/stories', false],
    ['/authorize?request=1', false],
    ['/', false],
  ])('isServerPath_WhenChecked_ShouldOnlyAcceptTheOauthPrefix (%s)', (value, expected) => {
    expect(isServerPath(value)).toBe(expected);
  });
});
