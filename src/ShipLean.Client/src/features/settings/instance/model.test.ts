import { expect, test } from 'vitest';
import { checkBaseUrl, pruneEdit, trimBaseUrl } from './model';

const saved = { name: 'A', baseUrl: 'https://a.test', projectCreation: 'everyone' } as const;

test('checkBaseUrl_WhenSameOriginWithTrailingSlashAndCase_ShouldMatch', () => {
  expect(checkBaseUrl('HTTPS://A.test/', 'https://a.test').matchesAddressInUse).toBe(true);
});

test('checkBaseUrl_WhenDifferentPort_ShouldNotMatch', () => {
  expect(checkBaseUrl('https://a.test', 'https://a.test:8080').matchesAddressInUse).toBe(false);
});

test('checkBaseUrl_WhenHttp_ShouldBeInsecure', () => {
  expect(checkBaseUrl('http://a.test', 'http://a.test')).toEqual({
    matchesAddressInUse: true,
    insecure: true,
  });
});

test('trimBaseUrl_WhenPaddedAndSlashed_ShouldTrimBoth', () => {
  expect(trimBaseUrl('  https://a.test//  ')).toBe('https://a.test');
});

test('pruneEdit_WhenFieldEqualsSaved_ShouldDropIt', () => {
  expect(pruneEdit(saved, { name: 'A', baseUrl: 'https://b.test' })).toEqual({
    baseUrl: 'https://b.test',
  });
});

test('checkBaseUrl_WhenDefaultPortIsTyped_ShouldMatch', () => {
  expect(checkBaseUrl('https://a.test:443', 'https://a.test').matchesAddressInUse).toBe(true);
  expect(checkBaseUrl('http://a.test:80/', 'http://a.test').matchesAddressInUse).toBe(true);
});

test('checkBaseUrl_WhenInternationalDomainIsTyped_ShouldMatchItsPunycodeOrigin', () => {
  expect(
    checkBaseUrl('https://bücher.example', 'https://xn--bcher-kva.example').matchesAddressInUse,
  ).toBe(true);
});

test('checkBaseUrl_WhenNotAnAddress_ShouldNotMatch', () => {
  expect(checkBaseUrl('not a url', 'https://a.test').matchesAddressInUse).toBe(false);
});
