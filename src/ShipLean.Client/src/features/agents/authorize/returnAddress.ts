// Schemes that are never a private-use redirect: http(s) keep their own rule, the rest can run script or read local data.
// Mirrors RedirectUriRules.BlockedSchemes in ShipLean.Application: change both together.
const blockedSchemes: ReadonlySet<string> = new Set([
  'http',
  'https',
  'javascript',
  'data',
  'file',
  'vbscript',
  'blob',
  'about',
  'ws',
  'wss',
  'ftp',
  'filesystem',
  'view-source',
  'mailto',
  'tel',
  'ms-msdt',
  'search-ms',
  'search',
  'ms-officecmd',
  'intent',
  'chrome',
  'chrome-extension',
  'jar',
]);

/** True for a scheme (with or without the trailing colon) an app registers for itself, such as cursor (RFC 8252 7.1). */
export function isPrivateUseScheme(scheme: string): boolean {
  const bare = scheme.endsWith(':') ? scheme.slice(0, -1) : scheme;
  return /^[a-z][a-z0-9+.-]*$/i.test(bare) && !blockedSchemes.has(bare.toLowerCase());
}

/** The address to send the browser to: only what the server returned, and only a web address or an app's private-use scheme. Null otherwise. */
export function returnAddress(redirectUrl: string): string | null {
  try {
    const url = new URL(redirectUrl);
    const web = url.protocol === 'http:' || url.protocol === 'https:';
    return web || isPrivateUseScheme(url.protocol) ? url.href : null;
  } catch {
    return null;
  }
}
