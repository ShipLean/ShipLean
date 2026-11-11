// The random parts of a sign-in (spec §4.3): a PKCE verifier and the state. The S256 challenge is the core's
// synchronous challengeFor; the only thing needed from the phone is random bytes, injected (expo-crypto's
// getRandomBytes in the app).
import { challengeFor } from '@shiplean/core/handoff';

export type RandomBytes = (count: number) => Uint8Array;

export interface Pkce {
  verifier: string;
  challenge: string;
  state: string;
}

// 64 characters, so six bits of each random byte pick one without bias.
const URL_SAFE = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
export const VERIFIER_LENGTH = 64;
export const STATE_LENGTH = 32;

function randomString(length: number, randomBytes: RandomBytes): string {
  const bytes = randomBytes(length);
  let out = '';
  for (let i = 0; i < length; i++) out += URL_SAFE[bytes[i] & 63];
  return out;
}

export function createPkce(randomBytes: RandomBytes): Pkce {
  const verifier = randomString(VERIFIER_LENGTH, randomBytes);
  return {
    verifier,
    challenge: challengeFor(verifier),
    state: randomString(STATE_LENGTH, randomBytes),
  };
}
