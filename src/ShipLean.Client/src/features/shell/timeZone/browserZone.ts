// The browser's own IANA zone ("Asia/Bangkok"), or null when it gives none. The one place the SPA may ask Intl for it
// (the shiplean/no-client-dates rule's permanent exception); the server makes every label from the stored zone.
export function browserZone(): string | null {
  const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  return zone ? zone : null;
}
