// The browser's own IANA zone ("Asia/Bangkok"), or null when it gives none or Intl throws. The one place the SPA may ask
// Intl for it (the shiplean/no-client-dates rule's permanent exception); the server makes every label from the stored zone.
// Asked once per page load: the first answer, even an empty one, is the answer every later caller gets.
let captured: { zone: string | null } | undefined;

export function browserZone(): string | null {
  if (captured === undefined) {
    try {
      const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
      captured = { zone: zone ? zone : null };
    } catch {
      captured = { zone: null };
    }
  }
  return captured.zone;
}
