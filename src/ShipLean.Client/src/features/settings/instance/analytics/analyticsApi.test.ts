import { expect, test } from 'vitest';
import settingsJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/analytics-settings.json';
import testResultJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/analytics-test-result.json';
import type { AnalyticsSettings, AnalyticsTestResult } from './analyticsApi';

test('AnalyticsApi_WhenPinnedSettingsRead_ShouldRoundTripThroughTheType', () => {
  const settings: AnalyticsSettings = settingsJson as AnalyticsSettings;
  expect(JSON.parse(JSON.stringify(settings))).toEqual(settingsJson);
  expect(['none', 'umami', 'posthog']).toContain(settings.provider);
  expect(Object.keys(settings).sort()).toEqual(['host', 'provider', 'sendNames', 'siteKey']);
});

test('AnalyticsApi_WhenPinnedTestResultRead_ShouldRoundTripThroughTheType', () => {
  const result: AnalyticsTestResult = testResultJson;
  expect(JSON.parse(JSON.stringify(result))).toEqual(testResultJson);
  expect(Object.keys(result).sort()).toEqual(['message', 'ok']);
});
