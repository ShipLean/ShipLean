import { expect, test } from '@playwright/experimental-ct-react';
import { AnalyticsHarness } from './ct/AnalyticsHarness';
import { analyticsUmami, stubAnalyticsApi } from './ct/analyticsStub';
import { analyticsTestIds as ids } from './testIds';

test('InstanceAnalytics_WhenOpened_ShouldMountTheTab', async ({ mount, page }) => {
  await stubAnalyticsApi(page);
  await mount(<AnalyticsHarness />);
  await expect(page.getByTestId(ids.container)).toBeVisible();
});

test('InstanceAnalytics_WhenOff_ShouldHideHostAndKey', async ({ mount, page }) => {
  await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await expect(view.getByTestId(ids.providerSelect)).toHaveValue('none');
  await expect(view.getByTestId(ids.hostInput)).toHaveCount(0);
  await expect(view.getByTestId(ids.siteKeyInput)).toHaveCount(0);
});

test('InstanceAnalytics_WhenUmamiChosen_ShouldShowWebsiteIdLabel', async ({ mount, page }) => {
  await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.providerSelect).selectOption('umami');
  await expect(view).toContainText('Website ID');
  await expect(view).not.toContainText('Project API key');
  await expect(view).toContainText('Your Umami address, such as https://umami.example.com');
  await expect(view.getByTestId(ids.hostInput)).toBeVisible();
});

test('InstanceAnalytics_WhenPostHogChosen_ShouldShowProjectApiKeyLabel', async ({
  mount,
  page,
}) => {
  await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.providerSelect).selectOption('posthog');
  await expect(view).toContainText('Project API key');
  await expect(view).not.toContainText('Website ID');
  await expect(view).toContainText('https://eu.i.posthog.com or https://us.i.posthog.com');
});

test('InstanceAnalytics_WhenSaved_ShouldPutTheC4Body', async ({ mount, page }) => {
  const stub = await stubAnalyticsApi(page);
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.providerSelect).selectOption('umami');
  await view.getByTestId(ids.hostInput).fill('https://umami.example.com');
  await view.getByTestId(ids.siteKeyInput).fill(analyticsUmami.siteKey);
  await view.getByTestId(ids.sendNamesToggle).check();
  await view.getByTestId(ids.saveButton).click();
  await expect(view.getByTestId(ids.saveButton)).toBeDisabled();
  await expect.poll(() => stub.puts).toEqual([{ ...analyticsUmami, sendNames: true }]);
});

test('InstanceAnalytics_WhenSwitchedOffAndBack_ShouldKeepTheDraftHost', async ({ mount, page }) => {
  await stubAnalyticsApi(page, { initial: analyticsUmami });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.providerSelect).selectOption('none');
  await view.getByTestId(ids.providerSelect).selectOption('umami');
  await expect(view.getByTestId(ids.hostInput)).toHaveValue(analyticsUmami.host);
  await expect(view.getByTestId(ids.siteKeyInput)).toHaveValue(analyticsUmami.siteKey);
});

test('InstanceAnalytics_WhenOffSaved_ShouldPutNullHostAndKey', async ({ mount, page }) => {
  const stub = await stubAnalyticsApi(page, { initial: analyticsUmami });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.providerSelect).selectOption('none');
  await view.getByTestId(ids.saveButton).click();
  await expect
    .poll(() => stub.puts)
    .toEqual([
      { provider: 'none', host: null, siteKey: null, sendNames: analyticsUmami.sendNames },
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
  await view.getByTestId(ids.providerSelect).selectOption('umami');
  await view.getByTestId(ids.hostInput).fill('ftp://nope');
  await view.getByTestId(ids.siteKeyInput).fill(analyticsUmami.siteKey);
  await view.getByTestId(ids.saveButton).click();
  await expect(view.getByTestId(ids.hostError)).toContainText('http or https');
  await expect(view.getByTestId(ids.fieldError)).toHaveCount(0);
  await expect(view.getByTestId(ids.hostInput)).toHaveValue('ftp://nope');
});

test('InstanceAnalytics_WhenTestOk_ShouldShowSuccessMessage', async ({ mount, page }) => {
  await stubAnalyticsApi(page, {
    initial: analyticsUmami,
    testResult: { ok: true, message: 'Test event sent to Umami.' },
  });
  const view = await mount(<AnalyticsHarness />);
  await view.getByTestId(ids.testButton).click();
  const result = view.getByTestId(ids.testResult);
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
  await view.getByTestId(ids.testButton).click();
  const result = view.getByTestId(ids.testResult);
  await expect(result).toHaveText('Umami answered 401.');
  await expect(result).toHaveAttribute('role', 'alert');
});

test('InstanceAnalytics_WhenUnsavedChanges_ShouldDisableTest', async ({ mount, page }) => {
  await stubAnalyticsApi(page, { initial: analyticsUmami });
  const view = await mount(<AnalyticsHarness />);
  await expect(view.getByTestId(ids.testButton)).toBeEnabled();
  await view.getByTestId(ids.sendNamesToggle).check();
  await expect(view.getByTestId(ids.testButton)).toBeDisabled();
  await expect(view).toContainText('Save first');
});
