import { api } from '@shiplean/core/api/client';

/** Wire values of the C4 contract. */
export type AnalyticsProvider = 'none' | 'umami' | 'posthog';

/** GET and PUT /api/instance/analytics: the PUT body has the same shape. */
export interface AnalyticsSettings {
  provider: AnalyticsProvider;
  host: string | null;
  siteKey: string | null;
  sendNames: boolean;
}

/** POST /api/instance/analytics/test: a send failure is `ok: false` with its reason, never an error response. */
export interface AnalyticsTestResult {
  ok: boolean;
  message: string;
}

export const analyticsKey = ['instance', 'analytics'] as const;

export function getAnalytics(): Promise<AnalyticsSettings> {
  return api.get<AnalyticsSettings>('/api/instance/analytics');
}

export function putAnalytics(body: AnalyticsSettings): Promise<AnalyticsSettings> {
  return api.put<AnalyticsSettings>('/api/instance/analytics', body);
}

export function testAnalytics(): Promise<AnalyticsTestResult> {
  return api.post<AnalyticsTestResult>('/api/instance/analytics/test');
}
