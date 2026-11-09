// What a person types in the Server field, as an origin (spec §4.1). Pure and hand-parsed: React Native's URL support
// is incomplete, and an address that only some runtimes parse would be a different address on each.

export const addressErrors = {
  empty: 'Enter the address of your ShipLean server.',
  scheme: 'The address must start with http:// or https://.',
  unparsable: "That doesn't look like a server address.",
  userinfo: 'Leave the user name and password out of the address.',
} as const;

export type ServerAddress =
  { ok: true; origin: string; host: string } | { ok: false; error: string };

const SCHEME = /^([A-Za-z][A-Za-z0-9+.-]*):\/\//;
const HOSTNAME = /^[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?$/;
const IPV6 = /^\[[0-9A-Fa-f:.]+\]$/;
const PORT = /^\d{1,5}$/;

// Trims; adds https:// when there is no scheme; keeps a typed http://; refuses any other scheme and a user name or
// password; drops the path, query, hash and trailing slash.
export function normaliseServerAddress(input: string): ServerAddress {
  const text = input.trim();
  if (text === '') return { ok: false, error: addressErrors.empty };

  const typed = SCHEME.exec(text);
  const scheme = typed ? typed[1].toLowerCase() : 'https';
  if (scheme !== 'http' && scheme !== 'https') return { ok: false, error: addressErrors.scheme };

  const rest = typed ? text.slice(typed[0].length) : text;
  const authority = rest.slice(0, rest.search(/[/?#]|$/));
  if (authority.includes('@')) return { ok: false, error: addressErrors.userinfo };

  const host = hostOf(authority);
  if (host === null) return { ok: false, error: addressErrors.unparsable };
  return { ok: true, origin: `${scheme}://${host}`, host };
}

// host[:port], lower-cased, or null when it isn't one.
function hostOf(authority: string): string | null {
  const colon = authority.startsWith('[') ? authority.indexOf(']') + 1 : authority.indexOf(':');
  const name = colon > 0 && colon < authority.length ? authority.slice(0, colon) : authority;
  const port = name === authority ? null : authority.slice(colon + 1);
  const portOk = port === null || (authority[colon] === ':' && PORT.test(port));
  if (!portOk) return null;
  if (!HOSTNAME.test(name) && !IPV6.test(name)) return null;
  return port === null ? name.toLowerCase() : `${name.toLowerCase()}:${port}`;
}

// The host part of an origin this module made ('https://shiplean.test' -> 'shiplean.test').
export function hostOfOrigin(origin: string): string {
  return origin.replace(SCHEME, '');
}
