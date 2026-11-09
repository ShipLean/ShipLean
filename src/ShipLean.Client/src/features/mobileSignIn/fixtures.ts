import { stubJson } from '../auth/authFixtures';

// Stubs for the component tests of the confirm page (page.route answers; nothing here is production code).
export const challenge = 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM';
export const stateValue = 'st-4f2a9c';
export const deviceName = 'Priya’s Pixel 9';

export function queryFor(device: string, overrides: Record<string, string> = {}): string {
  const values: Record<string, string> = { challenge, state: stateValue, device, ...overrides };
  return Object.entries(values)
    .map(([k, v]) => `${k}=${encodeURIComponent(v)}`)
    .join('&');
}

export const validQuery = queryFor(deviceName);

type Page = Parameters<typeof stubJson>[0];

export async function stubCodes(page: Page, code = 'code-123', status = 201) {
  await stubJson(page, '/api/auth/device-sessions/codes', { code }, status);
}
