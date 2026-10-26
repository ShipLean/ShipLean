import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../testIds';
import { settingsDialogTestIds as ids } from '../../features/settingsDialog/testIds';
import { SettingsDialogHarness } from './ct/SettingsDialogHarness';
import { settingsHarnessIds as harness } from './ct/harnessIds';

interface Doc {
  a: string;
  b: string;
  c: string;
}
interface Put {
  basedOn: Doc;
  settings: Doc;
}

interface StubbedRoute {
  request(): { postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

const FIRST: Doc = { a: 'a0', b: 'b0', c: 'c0' };

// Answers each PUT with the document it was sent (plus `merged`), recording the bodies.
async function stubPut(
  page: Routable,
  options: { merged?: string[]; gate?: Promise<void>; failFirst?: boolean } = {},
) {
  const puts: Put[] = [];
  let inFlight = 0;
  let overlapped = false;
  await page.route('**/api/fake/settings', async (route) => {
    inFlight += 1;
    if (inFlight > 1) overlapped = true;
    const body = route.request().postDataJSON() as Put;
    puts.push(body);
    if (puts.length === 1) await options.gate;
    inFlight -= 1;
    if (options.failFirst === true && puts.length === 1) {
      await route.fulfill({ status: 500, json: { error: 'Boom', code: 'internal_error' } });
      return;
    }
    await route.fulfill({
      json: { document: body.settings, mergedSections: options.merged ?? [] },
    });
  });
  return { puts, overlapped: () => overlapped };
}

test('Dialog_WhenPaneDirty_ShouldMarkNavAndFooter', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="onSave" />);
  await expect(page.getByTestId(ids.save)).toBeDisabled();
  await page.getByTestId(`${harness.input}-a`).fill('changed');
  await expect(page.getByTestId(`${ids.navDot}-a`)).toBeVisible();
  await expect(page.getByTestId(`${ids.navDot}-b`)).toHaveCount(0);
  await expect(page.getByTestId(ids.footerDirty)).toContainText('Pane A');
  await expect(page.getByTestId(ids.footerDirty)).not.toContainText('Pane B');
  await page.getByTestId(`${harness.input}-a`).fill('a0');
  await expect(page.getByTestId(`${ids.navDot}-a`)).toHaveCount(0);
  await expect(page.getByTestId(ids.footerDirty)).toHaveCount(0);
});

test('Dialog_WhenClosedDirty_ShouldAskSaveOrCancel', async ({ mount, page }) => {
  const stub = await stubPut(page);
  await mount(<SettingsDialogHarness apply="onSave" />);
  await page.getByTestId(`${harness.input}-a`).fill('changed');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.closeGuard)).toBeVisible();
  await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
  // focus moves to the first button of the guard
  await expect(page.getByTestId(ids.closeGuardSave)).toBeFocused();

  await page.getByTestId(ids.closeGuardSave).click();
  await expect(page.getByTestId(harness.closed)).toBeAttached();
  expect(stub.puts).toHaveLength(1);
  expect(stub.puts[0]?.settings.a).toBe('changed');
});

test('Dialog_WhenClosedDirtyAndCancelled_ShouldDropChangesWithoutSaving', async ({
  mount,
  page,
}) => {
  const stub = await stubPut(page);
  await mount(<SettingsDialogHarness apply="onSave" />);
  await page.getByTestId(`${harness.input}-a`).fill('changed');
  await page.getByTestId(componentTestIds.dialogClose).click();
  await page.getByTestId(ids.closeGuardDiscard).click();
  await expect(page.getByTestId(harness.closed)).toBeAttached();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Changes discarded');
  expect(stub.puts).toHaveLength(0);
});

test('Dialog_WhenCancelledWithoutChanges_ShouldCloseWithoutToast', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="onSave" />);
  await page.getByTestId(ids.cancel).click();
  await expect(page.getByTestId(harness.closed)).toBeAttached();
  await expect(page.getByTestId(componentTestIds.toast)).toHaveCount(0);
});

test('Dialog_WhenClosedClean_ShouldCloseAtOnce', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="onSave" />);
  await expect(page.getByTestId(componentTestIds.dialog)).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(harness.closed)).toBeAttached();
});

test('Dialog_WhenImmediate_ShouldPutPerChange', async ({ mount, page }) => {
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const stub = await stubPut(page, { gate });
  await mount(<SettingsDialogHarness apply="immediately" />);
  await expect(page.getByTestId(ids.save)).toHaveCount(0);

  await page.getByTestId(`${harness.input}-a`).fill('one');
  await expect.poll(() => stub.puts.length).toBe(1);
  // a change while the first is in flight waits for it
  await page.getByTestId(`${harness.input}-a`).fill('two');
  await expect(page.getByTestId(`${harness.input}-a`)).toHaveValue('two');
  release();

  await expect.poll(() => stub.puts.length).toBe(2);
  expect(stub.overlapped()).toBe(false);
  expect(stub.puts[0]?.settings).toEqual({ ...FIRST, a: 'one' });
  // the second is based on the document the first returned
  expect(stub.puts[1]?.basedOn).toEqual({ ...FIRST, a: 'one' });
  expect(stub.puts[1]?.settings).toEqual({ ...FIRST, a: 'two' });
});

test('Dialog_WhenMergedSections_ShouldToast', async ({ mount, page }) => {
  await stubPut(page, { merged: ['b'] });
  await mount(<SettingsDialogHarness apply="immediately" />);
  await page.getByTestId(`${harness.input}-a`).fill('one');
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Pane B');
});

test('Dialog_WhenSectionInUrl_ShouldOpenThatPane', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="onSave" />, { hooksConfig: { route: '/?settings=c' } });
  await expect(page.getByTestId(`${harness.pane}-c`)).toBeVisible();
  await expect(page.getByTestId(`${harness.pane}-a`)).toHaveCount(0);
  await page.getByTestId(`${ids.navItem}-b`).click();
  await expect(page.getByTestId(`${harness.pane}-b`)).toBeVisible();
});

test('Dialog_WhenSaved_ShouldToastAndClose', async ({ mount, page }) => {
  await stubPut(page);
  await mount(<SettingsDialogHarness apply="onSave" />);
  await page.getByTestId(`${harness.input}-a`).fill('changed');
  await page.getByTestId(ids.save).click();
  await expect(page.getByTestId(harness.closed)).toBeAttached();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Saved for the test');
});

test('Dialog_WhenMergedSectionsInSaveMode_ShouldToastTheirPaneLabels', async ({ mount, page }) => {
  await stubPut(page, { merged: ['b', 'c'] });
  await mount(<SettingsDialogHarness apply="onSave" />);
  await page.getByTestId(`${harness.input}-a`).fill('changed');
  await page.getByTestId(ids.save).click();
  const toast = page.getByTestId(componentTestIds.toast).filter({ hasText: 'Someone else' });
  await expect(toast).toContainText('Pane B, Pane C');
});

test('Dialog_WhenImmediateSaveFails_ShouldKeepTheChangeUnsavedAndResendOnRetry', async ({
  mount,
  page,
}) => {
  const pageErrors: string[] = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  const stub = await stubPut(page, { failFirst: true });
  await mount(<SettingsDialogHarness apply="immediately" />);
  await page.getByTestId(`${harness.input}-a`).fill('one');

  await expect(page.getByTestId(ids.footerError)).toHaveText('Boom');
  // not confirmed: the pane stays marked
  await expect(page.getByTestId(`${ids.navDot}-a`)).toBeVisible();
  await page.getByTestId(ids.footerRetry).click();

  await expect(page.getByTestId(`${ids.navDot}-a`)).toHaveCount(0);
  await expect(page.getByTestId(ids.footerError)).toHaveCount(0);
  expect(stub.puts).toHaveLength(2);
  expect(stub.puts[1]?.basedOn).toEqual(FIRST);
  expect(stub.puts[1]?.settings).toEqual({ ...FIRST, a: 'one' });
  expect(pageErrors).toEqual([]);
});

test('Dialog_WhenImmediateSaveFails_ShouldResendWithTheNextChange', async ({ mount, page }) => {
  const stub = await stubPut(page, { failFirst: true });
  await mount(<SettingsDialogHarness apply="immediately" />);
  await page.getByTestId(`${harness.input}-a`).fill('one');
  await expect(page.getByTestId(ids.footerError)).toBeVisible();
  await page.getByTestId(`${ids.navItem}-b`).click();
  await page.getByTestId(`${harness.input}-b`).fill('bee');

  await expect.poll(() => stub.puts.length).toBe(2);
  expect(stub.puts[1]?.settings).toEqual({ ...FIRST, a: 'one', b: 'bee' });
  await expect(page.getByTestId(ids.footerError)).toHaveCount(0);
});

test('Dialog_WhenImmediate_ShouldSayChangesApplyStraightAway', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="immediately" />);
  await expect(page.getByTestId(ids.immediateNote)).toHaveText('Changes apply straight away.');
});

test('Dialog_WhenSectionUnknown_ShouldOpenTheDefaultPane', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="onSave" />, {
    hooksConfig: { route: '/?settings=nope' },
  });
  await expect(page.getByTestId(`${harness.pane}-a`)).toBeVisible();
});

test('Dialog_WhenPaneDirty_ShouldTellScreenReaders', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="onSave" />);
  await page.getByTestId(`${harness.input}-a`).fill('changed');
  await expect(page.getByTestId(`${ids.navItem}-a`)).toHaveAccessibleName(/unsaved changes/);
  await expect(page.getByTestId(`${ids.navItem}-b`)).not.toHaveAccessibleName(/unsaved/);
  // each group is a list named by its label
  await expect(page.getByRole('list', { name: 'First' })).toBeVisible();
  await expect(page.getByRole('list', { name: 'Second' })).toBeVisible();
});

test('Dialog_WhenPaneWarns_ShouldMarkNavAndFooterFromTheDraft', async ({ mount, page }) => {
  await mount(<SettingsDialogHarness apply="onSave" />);
  await expect(page.getByTestId(`${ids.navWarning}-b`)).toHaveCount(0);
  await page.getByTestId(`${ids.navItem}-b`).click();
  await page.getByTestId(`${harness.input}-b`).fill('');
  await expect(page.getByTestId(`${ids.navItem}-b`)).toHaveAccessibleName(/has warnings/);
  await expect(page.getByTestId(`${ids.footerWarning}-b`)).toHaveText('Pane B is empty');
  await page.getByTestId(`${harness.input}-b`).fill('x');
  await expect(page.getByTestId(`${ids.footerWarning}-b`)).toHaveCount(0);
});
