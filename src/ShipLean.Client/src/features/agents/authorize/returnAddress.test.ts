import { expect, test } from 'vitest';
import { isPrivateUseScheme, returnAddress } from './returnAddress';

test('returnAddress_WhenHttpOrHttps_ShouldReturnIt', () => {
  expect(returnAddress('http://127.0.0.1:8123/callback?code=c&iss=i')).toBe(
    'http://127.0.0.1:8123/callback?code=c&iss=i',
  );
  expect(returnAddress('https://agent.example.test/cb?error=access_denied')).toBe(
    'https://agent.example.test/cb?error=access_denied',
  );
});

test('returnAddress_WhenNotAWebAddress_ShouldReturnNull', () => {
  expect(returnAddress('javascript:alert(1)')).toBeNull();
  expect(returnAddress('data:text/html,<b>x</b>')).toBeNull();
  expect(returnAddress('/relative/path')).toBeNull();
  expect(returnAddress('')).toBeNull();
});

test('returnAddress_WhenPrivateUseScheme_ShouldReturnIt', () => {
  expect(
    returnAddress('cursor://anysphere.cursor-retrieval/oauth/callback?code=c&state=s&iss=i'),
  ).toBe('cursor://anysphere.cursor-retrieval/oauth/callback?code=c&state=s&iss=i');
  expect(returnAddress('com.example.app:/oauth2redirect?code=c')).toBe(
    'com.example.app:/oauth2redirect?code=c',
  );
});

test.each([
  'javascript:alert(1)',
  'JAVASCRIPT:alert(1)',
  'data:text/html,<b>x</b>',
  'vbscript:x',
  'file:///etc/passwd',
  'blob:https://a.example/x',
  'about:blank',
  'ws://a.example/x',
  'wss://a.example/x',
  'ftp://a.example/x',
  'filesystem:https://a.example/x',
  'view-source:https://a.example/',
  'mailto:a@b.example',
  'tel:+15555550100',
  'ms-msdt:id',
  'search-ms:query=x',
  'intent://x#Intent;end',
  'chrome://settings',
  'jar:file:///a.jar!/',
])('returnAddress_WhenBlockedScheme_ShouldReturnNull %s', (address) => {
  expect(returnAddress(address)).toBeNull();
});

test.each([
  ['cursor', true],
  ['cursor:', true],
  ['Com.Example-App+1', true],
  ['1cursor', false],
  ['cur sor', false],
  ['', false],
  ['javascript', false],
  ['JavaScript:', false],
  ['data', false],
  ['file', false],
  ['vbscript', false],
  ['blob', false],
  ['about', false],
  ['ws', false],
  ['wss', false],
  ['ftp', false],
  ['filesystem', false],
  ['view-source', false],
  ['mailto', false],
  ['tel', false],
  ['ms-msdt', false],
  ['search-ms', false],
  ['search', false],
  ['ms-officecmd', false],
  ['intent', false],
  ['chrome', false],
  ['chrome-extension', false],
  ['jar', false],
  ['http', false],
  ['https', false],
])('isPrivateUseScheme_WhenGiven_%s_ShouldBe_%s', (scheme, expected) => {
  expect(isPrivateUseScheme(scheme)).toBe(expected);
});
