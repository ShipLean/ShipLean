// The anonymous server check (spec §4.2): GET {origin}/api/health. Pure; fetch is injected.
export type ServerCheck =
  | { state: 'ok'; version: string }
  | { state: 'notShipLean' }
  | { state: 'unreachable' }
  | { state: 'aborted' };

export interface ServerCheckOptions {
  fetch: typeof fetch;
  signal?: AbortSignal;
}

// ok only for a 200 whose body has status "ok" and a string version. A 200 with anything else is not a ShipLean
// server; no answer or an error status is unreachable. A check whose signal fired says so and nothing else.
export async function checkServer(
  origin: string,
  { fetch, signal }: ServerCheckOptions,
): Promise<ServerCheck> {
  try {
    const response = await fetch(`${origin}/api/health`, { credentials: 'omit', signal });
    if (!response.ok) return { state: 'unreachable' };
    const body: unknown = await response.json().catch(() => null);
    if (isHealthy(body)) return { state: 'ok', version: body.version };
    return { state: 'notShipLean' };
  } catch {
    return signal?.aborted ? { state: 'aborted' } : { state: 'unreachable' };
  }
}

function isHealthy(body: unknown): body is { status: 'ok'; version: string } {
  if (typeof body !== 'object' || body === null) return false;
  const { status, version } = body as Record<string, unknown>;
  return status === 'ok' && typeof version === 'string';
}

// The line under the field; null while there is nothing to say.
export function checkLine(check: ServerCheck, host: string): string | null {
  switch (check.state) {
    case 'ok':
      return `ShipLean ${check.version.split('+')[0]} · connected`;
    case 'notShipLean':
      return `${host} isn't a ShipLean server. Check the address.`;
    case 'unreachable':
      return `Can't reach ${host}. Check the address and that the server is running.`;
    case 'aborted':
      return null;
  }
}
