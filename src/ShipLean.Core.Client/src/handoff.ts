// The browser handoff that signs the phone app in. The app opens the server's confirm page with a PKCE challenge, a
// state and its device name; the page sends the browser back to the app's fixed redirect with a code (or why not),
// plus its own origin as `iss`, so the app can refuse a code that another server's page minted (RFC 9207's mix-up
// defence).
// Hand-parsed rather than through URL/URLSearchParams, because React Native's URL support is incomplete. No
// randomness here: the app makes the verifier and the state.

export const APP_REDIRECT = 'shiplean://signed-in';
export const CONFIRM_PATH = '/mobile/sign-in';

export interface ConfirmQuery {
  challenge: string;
  state: string;
  device: string;
}

export type ConfirmQueryResult = ConfirmQuery | { error: 'missing' };

export type AppRedirectError = 'cancelled' | 'unsupported';

// iss is the origin of the server whose confirm page sent the browser back: scheme, host and port, with no path.
export type AppRedirect =
  | { code: string; state: string; iss: string }
  | { error: AppRedirectError; state: string; iss: string };

const MAX_STATE = 128;
const MAX_DEVICE = 100;
const CHALLENGE = /^[A-Za-z0-9_-]{43}$/;
const VERIFIER = /^[A-Za-z0-9\-._~]{43,128}$/;
// An origin as window.location.origin gives it: http(s), a host name or a bracketed IPv6 address, an optional port, and
// nothing after (no path, query, fragment or user info).
const ORIGIN = /^https?:\/\/(?:[A-Za-z0-9.-]+|\[[0-9A-Fa-f:.]+\])(?::\d{1,5})?$/;

function queryString(pairs: [string, string][]): string {
  return pairs.map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');
}

// A query string (with or without its leading '?') as a map; null when a value isn't valid percent-encoding.
function parseQuery(query: string): Map<string, string> | null {
  const values = new Map<string, string>();
  const text = query.startsWith('?') ? query.slice(1) : query;
  if (text === '') return values;
  for (const part of text.split('&')) {
    const at = part.indexOf('=');
    const rawKey = at < 0 ? part : part.slice(0, at);
    const rawValue = at < 0 ? '' : part.slice(at + 1);
    try {
      const key = decodeURIComponent(rawKey.replace(/\+/g, ' '));
      // The first value of a repeated key wins, as URLSearchParams.get does.
      if (!values.has(key)) values.set(key, decodeURIComponent(rawValue.replace(/\+/g, ' ')));
    } catch {
      return null;
    }
  }
  return values;
}

export function confirmUrl(origin: string, query: ConfirmQuery): string {
  return `${origin}${CONFIRM_PATH}?${queryString([
    ['challenge', query.challenge],
    ['state', query.state],
    ['device', query.device],
  ])}`;
}

// The confirm page's query. The device name comes back trimmed; it is only ever shown, never trusted.
export function parseConfirmQuery(query: string): ConfirmQueryResult {
  const values = parseQuery(query);
  const challenge = values?.get('challenge') ?? '';
  const state = values?.get('state') ?? '';
  const device = (values?.get('device') ?? '').trim();
  if (!CHALLENGE.test(challenge)) return { error: 'missing' };
  if (state === '' || state.length > MAX_STATE) return { error: 'missing' };
  if (device === '' || device.length > MAX_DEVICE) return { error: 'missing' };
  return { challenge, state, device };
}

export function appRedirect(redirect: AppRedirect): string {
  return 'code' in redirect
    ? `${APP_REDIRECT}?${queryString([
        ['code', redirect.code],
        ['state', redirect.state],
        ['iss', redirect.iss],
      ])}`
    : `${APP_REDIRECT}?${queryString([
        ['error', redirect.error],
        ['state', redirect.state],
        ['iss', redirect.iss],
      ])}`;
}

// What the browser handed back to the app; null for anything that isn't exactly one of appRedirect's URLs, including
// one whose iss is missing or isn't a bare origin. Whether iss is the server the app meant is the caller's check.
export function parseAppRedirect(url: string): AppRedirect | null {
  const prefix = `${APP_REDIRECT}?`;
  if (!url.startsWith(prefix)) return null;
  const hash = url.indexOf('#');
  const values = parseQuery(url.slice(prefix.length, hash < 0 ? undefined : hash));
  const state = values?.get('state');
  if (values === null || state === undefined || state === '') return null;
  const iss = values.get('iss');
  if (iss === undefined || !ORIGIN.test(iss)) return null;
  const code = values.get('code');
  const error = values.get('error');
  if (error !== undefined) {
    if (code !== undefined) return null;
    return error === 'cancelled' || error === 'unsupported' ? { error, state, iss } : null;
  }
  return code !== undefined && code !== '' ? { code, state, iss } : null;
}

// RFC 7636: a verifier is 43–128 unreserved characters.
export function isVerifier(s: string): boolean {
  return VERIFIER.test(s);
}

// The S256 challenge for a verifier: base64url(SHA-256(ASCII verifier)), no padding. Synchronous and pure TypeScript,
// because Hermes has no crypto.subtle.
export function challengeFor(verifier: string): string {
  return base64Url(sha256(utf8(verifier)));
}

function utf8(text: string): Uint8Array {
  return new TextEncoder().encode(text);
}

const BASE64URL = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';

function base64Url(bytes: Uint8Array): string {
  let out = '';
  for (let i = 0; i < bytes.length; i += 3) {
    const n = (bytes[i] << 16) | ((bytes[i + 1] ?? 0) << 8) | (bytes[i + 2] ?? 0);
    const chars = Math.min(4, Math.ceil(((bytes.length - i) * 8) / 6));
    for (let c = 0; c < chars; c++) out += BASE64URL[(n >>> (18 - 6 * c)) & 63];
  }
  return out;
}

// FIPS 180-4 round constants: the first 32 bits of the fractional parts of the cube roots of the first 64 primes.
const K = new Uint32Array([
  0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
  0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
  0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
  0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
  0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
  0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
  0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
  0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
]);

const rotr = (x: number, n: number) => (x >>> n) | (x << (32 - n));

function sha256(message: Uint8Array): Uint8Array {
  // Pad: a 1 bit, zeros, then the bit length as a 64-bit big-endian number, to a multiple of 64 bytes.
  const blocks = Math.ceil((message.length + 9) / 64);
  const padded = new Uint8Array(blocks * 64);
  padded.set(message);
  padded[message.length] = 0x80;
  const view = new DataView(padded.buffer);
  const bits = message.length * 8;
  view.setUint32(padded.length - 8, Math.floor(bits / 0x100000000));
  view.setUint32(padded.length - 4, bits >>> 0);

  const h = new Uint32Array([
    0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
  ]);
  const w = new Uint32Array(64);
  for (let block = 0; block < blocks; block++) {
    for (let t = 0; t < 16; t++) w[t] = view.getUint32(block * 64 + t * 4);
    for (let t = 16; t < 64; t++) {
      const s0 = rotr(w[t - 15], 7) ^ rotr(w[t - 15], 18) ^ (w[t - 15] >>> 3);
      const s1 = rotr(w[t - 2], 17) ^ rotr(w[t - 2], 19) ^ (w[t - 2] >>> 10);
      w[t] = w[t - 16] + s0 + w[t - 7] + s1;
    }
    let [a, b, c, d, e, f, g, hh] = h;
    for (let t = 0; t < 64; t++) {
      const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
      const ch = (e & f) ^ (~e & g);
      const t1 = (hh + S1 + ch + K[t] + w[t]) >>> 0;
      const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const t2 = (S0 + maj) >>> 0;
      hh = g;
      g = f;
      f = e;
      e = (d + t1) >>> 0;
      d = c;
      c = b;
      b = a;
      a = (t1 + t2) >>> 0;
    }
    h[0] += a;
    h[1] += b;
    h[2] += c;
    h[3] += d;
    h[4] += e;
    h[5] += f;
    h[6] += g;
    h[7] += hh;
  }
  const out = new Uint8Array(32);
  const outView = new DataView(out.buffer);
  for (let i = 0; i < 8; i++) outView.setUint32(i * 4, h[i]);
  return out;
}
