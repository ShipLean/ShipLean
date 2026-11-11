import { getRandomValues, randomUUID } from 'expo-crypto';

// Hermes has no Web Crypto. The core calls crypto.randomUUID and crypto.getRandomValues (request ids, PKCE), and
// reads them when its modules load, so index.ts imports this file before anything else. Only what is missing is filled.
const target = globalThis as { crypto?: Partial<Crypto> };
const webCrypto: Partial<Crypto> = target.crypto ?? (target.crypto = {});

if (typeof webCrypto.randomUUID !== 'function') {
  webCrypto.randomUUID = randomUUID as Crypto['randomUUID'];
}
if (typeof webCrypto.getRandomValues !== 'function') {
  webCrypto.getRandomValues = getRandomValues as Crypto['getRandomValues'];
}
