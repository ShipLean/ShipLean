import { challengeFor, isVerifier } from '@shiplean/core/handoff';
import { expect, test } from 'vitest';
import { createPkce, STATE_LENGTH, VERIFIER_LENGTH } from './pkce';

const counting = (count: number) => Uint8Array.from({ length: count }, (_, i) => i * 37 + 11);

test('createPkce_ShouldMakeA64CharUrlSafeVerifier', () => {
  const { verifier } = createPkce(counting);
  expect(verifier).toHaveLength(VERIFIER_LENGTH);
  expect(verifier).toMatch(/^[A-Za-z0-9_-]{64}$/);
  expect(isVerifier(verifier)).toBe(true);
});

test('createPkce_ShouldMakeAUrlSafeState', () => {
  const { state } = createPkce(counting);
  expect(state).toHaveLength(STATE_LENGTH);
  expect(state).toMatch(/^[A-Za-z0-9_-]+$/);
});

test('createPkce_WhenBytesDiffer_ShouldMakeADifferentVerifierAndState', () => {
  let next = 0;
  const random = (count: number) => Uint8Array.from({ length: count }, () => next++ * 7);
  const a = createPkce(random);
  const b = createPkce(random);
  expect(a.verifier).not.toBe(b.verifier);
  expect(a.state).not.toBe(b.state);
  expect(a.verifier).not.toBe(a.state);
});

test('createPkce_ShouldChallengeTheVerifierWithS256', () => {
  const pkce = createPkce(counting);
  expect(pkce.challenge).toBe(challengeFor(pkce.verifier));
  expect(pkce.challenge).toMatch(/^[A-Za-z0-9_-]{43}$/);
});

test('challengeFor_WhenGivenTheRfc7636Verifier_ShouldMatchItsChallenge', () => {
  expect(challengeFor('dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk')).toBe(
    'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM',
  );
});
