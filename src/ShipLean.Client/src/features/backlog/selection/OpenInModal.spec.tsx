import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { OpenInModalHarness } from '../ct/OpenInModalHarness';
import { itemPeekTestIds } from '../../itemPeek/testIds';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const row = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`);
const address = (page: Page) => page.getByTestId('harness-address');

test('Title_click_on_backlog_opens_the_modal', async ({ mount, page }) => {
  await mount(<OpenInModalHarness />, route);

  await row(page, 'SHIP-2').getByTestId(ids.title).click();

  await expect(page.getByTestId(itemPeekTestIds.dialog)).toBeVisible();
  await expect(address(page)).toHaveText('/p/SHIP/backlogs/stories?item=SHIP-2');
});

test('Ctrl_click_keeps_the_new_tab_href', async ({ mount, page }) => {
  await mount(<OpenInModalHarness />, route);
  const title = row(page, 'SHIP-2').getByTestId(ids.title);
  await expect(title).toHaveAttribute('href', '/p/SHIP/items/SHIP-2');

  // A modified click is the browser's: with no selection it falls through to a new tab, so nothing here changes.
  const handled = page.evaluate(
    () =>
      new Promise<boolean>((resolve) => {
        document.addEventListener('click', (event) => resolve(event.ctrlKey), { once: true });
      }),
  );
  await title.click({ modifiers: ['Control'] });
  expect(await handled).toBe(true);

  await expect(page.getByTestId(itemPeekTestIds.dialog)).toHaveCount(0);
  await expect(address(page)).toHaveText('/p/SHIP/backlogs/stories');
});

test('Open_command_on_focused_row_opens_the_modal', async ({ mount, page }) => {
  await mount(<OpenInModalHarness />, route);
  await row(page, 'SHIP-3').getByTestId(ids.rank).click();
  await expect(page.getByTestId(itemPeekTestIds.dialog)).toHaveCount(0);

  await page.getByTestId('run-command-open').click();

  await expect(page.getByTestId(itemPeekTestIds.dialog)).toBeVisible();
  await expect(address(page)).toHaveText('/p/SHIP/backlogs/stories?item=SHIP-3');
});

test('Open_command_without_a_focused_row_does_nothing', async ({ mount, page }) => {
  await mount(<OpenInModalHarness />, route);

  await page.getByTestId('run-command-open').click();

  await expect(page.getByTestId(itemPeekTestIds.dialog)).toHaveCount(0);
  await expect(address(page)).toHaveText('/p/SHIP/backlogs/stories');
});

test('Closing_returns_focus_to_the_row', async ({ mount, page }) => {
  await mount(<OpenInModalHarness />, route);
  await row(page, 'SHIP-2').getByTestId(ids.title).click();
  await expect(page.getByTestId(itemPeekTestIds.dialog)).toBeVisible();

  await page.getByTestId(itemPeekTestIds.close).click();

  await expect(page.getByTestId(itemPeekTestIds.dialog)).toHaveCount(0);
  await expect(
    page.locator(`[data-testid="${ids.row}"][data-key="SHIP-2"]:focus-within`),
  ).toHaveCount(1);
  await expect(address(page)).toHaveText('/p/SHIP/backlogs/stories');
});
