import { api } from '@shiplean/core/api/client';
import { platform } from '@shiplean/core/platform';

/** One page's readings, as POST /api/usage/web-vitals takes them. A metric not measured is null. */
export interface WebVitalsReport {
  screen: string;
  navigation: 'hard' | 'soft';
  lcp: number | null;
  inp: number | null;
  cls: number | null;
  fcp: number | null;
  ttfb: number | null;
  screenSize: string | null;
}

/** GET /api/usage/web-vitals/config: true only when the instance switch is on and an analytics provider is set. */
export interface WebVitalsConfig {
  enabled: boolean;
}

export const webVitalsConfigKey = ['webVitalsConfig'] as const;

export function getWebVitalsConfig(): Promise<WebVitalsConfig> {
  return api.get<WebVitalsConfig>('/api/usage/web-vitals/config');
}

/**
 * Sends readings with fetch keepalive, so the request survives the page closing. It does not go through `api.post`:
 * a send during page close must not redirect to sign-in or show anything, so every failure, a refused request
 * included, is dropped. Readings are lossy by design and are not retried.
 */
export async function postWebVitals(reports: WebVitalsReport[]): Promise<void> {
  try {
    await fetch(platform().apiOrigin() + '/api/usage/web-vitals', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reports }),
      keepalive: true,
      credentials: 'same-origin',
    });
  } catch {
    // Dropped on purpose: see above.
  }
}
