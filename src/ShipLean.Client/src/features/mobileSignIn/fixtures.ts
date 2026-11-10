import { CONFIRM_PATH, confirmUrl, type ConfirmQuery } from '@shiplean/core/handoff';
import { stubJson } from '../auth/authFixtures';

// Stubs for the component tests of the confirm page (page.route answers; nothing here is production code).
export const challenge = 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM';
export const stateValue = 'st-4f2a9c';
export const deviceName = 'Priya’s Pixel 9';

// The confirm page's query string (no leading '?'), as the app builds it.
export function queryFor(device: string, overrides: Partial<ConfirmQuery> = {}): string {
  const url = confirmUrl('', { challenge, state: stateValue, device, ...overrides });
  return url.slice(`${CONFIRM_PATH}?`.length);
}

export const validQuery = queryFor(deviceName);

type Page = Parameters<typeof stubJson>[0];

export async function stubCodes(page: Page, code = 'code-123', status = 201) {
  await stubJson(page, '/api/auth/device-sessions/codes', { code }, status);
}
