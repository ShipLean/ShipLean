import { expect, test } from 'vitest';
import {
  APP_REDIRECT,
  appRedirect,
  challengeFor,
  confirmUrl,
  isVerifier,
  parseAppRedirect,
  parseConfirmQuery,
} from './handoff';

// RFC 7636 appendix B.
const RFC_VERIFIER = 'dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk';
const RFC_CHALLENGE = 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM';

const query = { challenge: RFC_CHALLENGE, state: 'st-8Kq2_x', device: "Joel's iPhone & co" };

function searchOf(url: string): string {
  return url.slice(url.indexOf('?'));
}

test('confirmUrl_ShouldPointAtTheConfirmPageWithEveryValueEncoded', () => {
  const url = confirmUrl('https://ship.example.com:8443', query);

  expect(url).toBe(
    'https://ship.example.com:8443/mobile/sign-in?challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM' +
      "&state=st-8Kq2_x&device=Joel's%20iPhone%20%26%20co",
  );
});

test('parseConfirmQuery_WhenGivenConfirmUrlsQuery_ShouldRoundTrip', () => {
  const search = searchOf(confirmUrl('https://ship.example.com', query));

  expect(parseConfirmQuery(search)).toEqual(query);
  expect(parseConfirmQuery(search.slice(1))).toEqual(query);
});

test('parseConfirmQuery_WhenTheDeviceHasSpacesAround_ShouldTrimIt', () => {
  const search = searchOf(confirmUrl('', { ...query, device: '  Pixel 8  ' }));

  expect(parseConfirmQuery(search)).toEqual({ ...query, device: 'Pixel 8' });
});

test('parseConfirmQuery_WhenTheQueryUsesPlusForSpaces_ShouldReadThemAsSpaces', () => {
  const search = `?challenge=${RFC_CHALLENGE}&state=s&device=Pixel+8`;

  expect(parseConfirmQuery(search)).toEqual({
    challenge: RFC_CHALLENGE,
    state: 's',
    device: 'Pixel 8',
  });
});

test.each([
  ['a short challenge', { challenge: RFC_CHALLENGE.slice(1) }],
  ['a long challenge', { challenge: RFC_CHALLENGE + 'A' }],
  ['bad challenge characters', { challenge: RFC_CHALLENGE.slice(0, 42) + '=' }],
  ['a padded-out challenge', { challenge: RFC_CHALLENGE.slice(0, 42) + '+' }],
  ['an empty state', { state: '' }],
  ['a state over 128', { state: 's'.repeat(129) }],
  ['a long device', { device: 'd'.repeat(101) }],
  ['a blank device', { device: '   ' }],
])('parseConfirmQuery_WhenAValueIsBad_ShouldBeMissing (%s)', (_, change) => {
  const search = searchOf(confirmUrl('', { ...query, ...change }));

  expect(parseConfirmQuery(search)).toEqual({ error: 'missing' });
});

test('parseConfirmQuery_WhenValuesAreAtTheLimits_ShouldAccept', () => {
  const atLimits = { challenge: RFC_CHALLENGE, state: 's'.repeat(128), device: 'd'.repeat(100) };

  expect(parseConfirmQuery(searchOf(confirmUrl('', atLimits)))).toEqual(atLimits);
});

test.each([
  ['no query', ''],
  ['no challenge', '?state=s&device=Pixel'],
  ['no state', `?challenge=${RFC_CHALLENGE}&device=Pixel`],
  ['no device', `?challenge=${RFC_CHALLENGE}&state=s`],
  ['broken percent-encoding', `?challenge=${RFC_CHALLENGE}&state=s&device=%E0%A4%A`],
])('parseConfirmQuery_WhenAValueIsAbsent_ShouldBeMissing (%s)', (_, search) => {
  expect(parseConfirmQuery(search)).toEqual({ error: 'missing' });
});

test('appRedirect_WhenGivenACode_ShouldRoundTrip', () => {
  const url = appRedirect({ code: 'c0de/+=', state: 'st 1' });

  expect(url).toBe('shiplean://signed-in?code=c0de%2F%2B%3D&state=st%201');
  expect(url.startsWith(APP_REDIRECT)).toBe(true);
  expect(parseAppRedirect(url)).toEqual({ code: 'c0de/+=', state: 'st 1' });
});

test.each([['cancelled' as const], ['unsupported' as const]])(
  'appRedirect_WhenGivenAnError_ShouldRoundTrip (%s)',
  (error) => {
    const url = appRedirect({ error, state: 'st-1' });

    expect(url).toBe(`shiplean://signed-in?error=${error}&state=st-1`);
    expect(parseAppRedirect(url)).toEqual({ error, state: 'st-1' });
  },
);

test.each([
  ['another scheme', 'https://signed-in?code=c&state=s'],
  ['another scheme, same host', 'exp://signed-in?code=c&state=s'],
  ['another path', 'shiplean://signed-out?code=c&state=s'],
  ['a longer path', 'shiplean://signed-in/extra?code=c&state=s'],
  ['a prefix of the path', 'shiplean://signed-inside?code=c&state=s'],
  ['no query', 'shiplean://signed-in'],
  ['no state', 'shiplean://signed-in?code=c'],
  ['an empty state', 'shiplean://signed-in?code=c&state='],
  ['no code or error', 'shiplean://signed-in?state=s'],
  ['an empty code', 'shiplean://signed-in?code=&state=s'],
  ['an unknown error', 'shiplean://signed-in?error=denied&state=s'],
  ['both a code and an error', 'shiplean://signed-in?code=c&error=cancelled&state=s'],
  ['broken percent-encoding', 'shiplean://signed-in?code=%zz&state=s'],
])('parseAppRedirect_WhenNotOneOfOurUrls_ShouldBeNull (%s)', (_, url) => {
  expect(parseAppRedirect(url)).toBeNull();
});

test('challengeFor_ShouldMatchTheRfc7636Vector', () => {
  expect(challengeFor(RFC_VERIFIER)).toBe(RFC_CHALLENGE);
});

test('challengeFor_WhenTheVerifierIs128Characters_ShouldHashAcrossBlocks', () => {
  const verifier = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~'
    .repeat(2)
    .slice(0, 128);

  // Node: createHash('sha256').update(verifier).digest('base64url')
  expect(challengeFor(verifier)).toBe('g5qy6ByDJPNTNnMNf87wCyaqLMq1mtSaSMtvwRxIZdE');
  expect(isVerifier(verifier)).toBe(true);
});

test('isVerifier_ShouldAcceptOnly43To128UnreservedCharacters', () => {
  expect(isVerifier(RFC_VERIFIER)).toBe(true);
  expect(isVerifier('a'.repeat(43))).toBe(true);
  expect(isVerifier('a'.repeat(42))).toBe(false);
  expect(isVerifier('a'.repeat(129))).toBe(false);
  expect(isVerifier('a'.repeat(42) + '+')).toBe(false);
  expect(isVerifier('a'.repeat(42) + '=')).toBe(false);
  expect(isVerifier('-._~'.repeat(11))).toBe(true);
});
