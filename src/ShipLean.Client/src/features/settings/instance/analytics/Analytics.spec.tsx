import { expect, test } from '@playwright/experimental-ct-react';
import { AnalyticsHarness } from './ct/AnalyticsHarness';
import { analyticsNone, analyticsUmami, stubAnalyticsApi } from './ct/analyticsStub';
import { settingsTestIds as ids } from '../../testIds';

test('InstanceAnalytics_WhenOpened_ShouldMountTheTab', async ({ mount, page }) => {
  await stubAnalyticsApi(page);
  await mount(<AnalyticsHarness />);
  await expect(page.getByTestId(ids.analyticsContainer)).toBeVisible();
});

test('InstanceAnalytics_WhenOff_ShouldHideHostAndKey', async ({ mount, page }) => {
  await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await expect(view.getByTestId(ids.analyticsProviderSelect)).toHaveValue('none');
  await expect(view.getByTestId(ids.analyticsHostInput)).toHaveCount(0);
  await expect(view.getByTestId(ids.analyticsSiteKeyInput)).toHaveCount(0);
});

test('InstanceAnalytics_WhenUmamiChosen_ShouldShowWebsiteIdLabel', async ({ mount, page }) => {
  await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsProviderSelect).selectOption('umami');
  await expect(view).toContainText('Website ID');
  await expect(view).not.toContainText('Project API key');
  await expect(view).toContainText('Your Umami address, such as https://umami.example.com');
  await expect(view.getByTestId(ids.analyticsHostInput)).toBeVisible();
});

test('InstanceAnalytics_WhenPostHogChosen_ShouldShowProjectApiKeyLabel', async ({
  mount,
  page,
}) => {
  await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsProviderSelect).selectOption('posthog');
  await expect(view).toContainText('Project API key');
  await expect(view).not.toContainText('Website ID');
  await expect(view).toContainText('https://eu.i.posthog.com or https://us.i.posthog.com');
});

test('InstanceAnalytics_WhenSaved_ShouldPutTheC4Body', async ({ mount, page }) => {
  const stub = await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsProviderSelect).selectOption('umami');
  await view.getByTestId(ids.analyticsHostInput).fill('https://umami.example.com');
  await view.getByTestId(ids.analyticsSiteKeyInput).fill(analyticsUmami.siteKey);
  await view.getByTestId(ids.analyticsSendNamesToggle).check();
  await view.getByTestId(ids.analyticsSaveButton).click();
  await expect(view.getByTestId(ids.analyticsSaveButton)).toBeDisabled();
  await expect.poll(() => stub.puts).toEqual([{ ...analyticsUmami, sendNames: true }]);
});

test('InstanceAnalytics_WhenSwitchedOffAndBack_ShouldKeepTheDraftHost', async ({ mount, page }) => {
  await stubAnalyticsApi(page, { initial: analyticsUmami });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsProviderSelect).selectOption('none');
  await view.getByTestId(ids.analyticsProviderSelect).selectOption('umami');
  await expect(view.getByTestId(ids.analyticsHostInput)).toHaveValue(analyticsUmami.host);
  await expect(view.getByTestId(ids.analyticsSiteKeyInput)).toHaveValue(analyticsUmami.siteKey);
});

test('InstanceAnalytics_WhenOffSaved_ShouldPutNullHostAndKey', async ({ mount, page }) => {
  const stub = await stubAnalyticsApi(page, { initial: analyticsUmami });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsProviderSelect).selectOption('none');
  await view.getByTestId(ids.analyticsSaveButton).click();
  await expect
    .poll(() => stub.puts)
    .toEqual([
      {
        provider: 'none',
        host: null,
        siteKey: null,
        sendNames: analyticsUmami.sendNames,
        sendWebVitals: false,
      },
    ]);
});

test('InstanceAnalytics_WhenServerRejectsHost_ShouldShowErrorUnderHost', async ({
  mount,
  page,
}) => {
  await stubAnalyticsApi(page, {
    rejectPut: { error: 'Host must be an http or https address.', field: 'host' },
  });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsProviderSelect).selectOption('umami');
  await view.getByTestId(ids.analyticsHostInput).fill('ftp://nope');
  await view.getByTestId(ids.analyticsSiteKeyInput).fill(analyticsUmami.siteKey);
  await view.getByTestId(ids.analyticsSaveButton).click();
  await expect(view.getByTestId(ids.analyticsHostError)).toContainText('http or https');
  await expect(view.getByTestId(ids.analyticsFieldError)).toHaveCount(0);
  await expect(view.getByTestId(ids.analyticsHostInput)).toHaveValue('ftp://nope');
});

test('InstanceAnalytics_WhenTestOk_ShouldShowSuccessMessage', async ({ mount, page }) => {
  await stubAnalyticsApi(page, {
    initial: analyticsUmami,
    testResult: { ok: true, message: 'Test event sent to Umami.' },
  });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsTestButton).click();
  const result = view.getByTestId(ids.analyticsTestResult);
  await expect(result).toHaveText('Test event sent to Umami.');
  await expect(result).toHaveAttribute('role', 'status');
  await expect(result).toHaveClass(/text-ok/);
});

test('InstanceAnalytics_WhenTestFails_ShouldShowReason', async ({ mount, page }) => {
  await stubAnalyticsApi(page, {
    initial: analyticsUmami,
    testResult: { ok: false, message: 'Umami answered 401.' },
  });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.analyticsTestButton).click();
  const result = view.getByTestId(ids.analyticsTestResult);
  await expect(result).toHaveText('Umami answered 401.');
  await expect(result).toHaveAttribute('role', 'alert');
});

test('InstanceAnalytics_WhenUnsavedChanges_ShouldDisableTest', async ({ mount, page }) => {
  await stubAnalyticsApi(page, { initial: analyticsUmami });
  const view = await mount(<AnalyticsHarness />);
  await expect(view.getByTestId(ids.analyticsTestButton)).toBeEnabled();
  await view.getByTestId(ids.analyticsSendNamesToggle).check();
  await expect(view.getByTestId(ids.analyticsTestButton)).toBeDisabled();
  await expect(view).toContainText('Save first');
});

test('InstanceAnalytics_WhenWebVitalsTurnedOn_ShouldPutTrue', async ({ mount, page }) => {
  const stub = await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  const toggle = view.getByTestId(ids.analyticsSendWebVitalsToggle);
  await expect(toggle).not.toBeChecked();
  await toggle.check();
  await view.getByTestId(ids.analyticsSaveButton).click();
  await expect.poll(() => stub.puts).toEqual([{ ...analyticsNone, sendWebVitals: true }]);
  await expect(toggle).toBeChecked();
  await expect(view.getByTestId(ids.analyticsSaveButton)).toBeDisabled();
});

test('InstanceAnalytics_WhenLoadedWithWebVitalsOn_ShouldShowItChecked', async ({ mount, page }) => {
  await stubAnalyticsApi(page, { initial: { ...analyticsUmami, sendWebVitals: true } });
  const view = await mount(<AnalyticsHarness />);
  await expect(view.getByTestId(ids.analyticsSendWebVitalsToggle)).toBeChecked();
  await expect(view.getByTestId(ids.analyticsSaveButton)).toBeDisabled();
});

test('InstanceAnalytics_WhenLocked_ShouldDisableWebVitals', async ({ mount, page }) => {
  await stubAnalyticsApi(page, { initial: analyticsUmami });
  // Hold the save open until the test lets it go; a later route runs before the stub's.
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/instance/analytics', async (route) => {
    if (route.request().method() === 'PUT') await gate;
    await route.fallback();
  });
  const view = await mount(<AnalyticsHarness />);
  const toggle = view.getByTestId(ids.analyticsSendWebVitalsToggle);
  await expect(toggle).toBeEnabled();
  await toggle.check();
  await view.getByTestId(ids.analyticsSaveButton).click();
  await expect(toggle).toBeDisabled();
  release();
  await expect(toggle).toBeEnabled();
});
