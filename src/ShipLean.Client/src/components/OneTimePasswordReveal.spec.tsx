import { expect, test } from '@playwright/experimental-ct-react';
import { OneTimePasswordReveal } from './OneTimePasswordReveal';
import { componentTestIds as ids } from './testIds';

test('OneTimePasswordReveal_WhenDone_ShouldRemoveThePassword', async ({ mount, page }) => {
  let done = 0;
  await mount(<OneTimePasswordReveal password="correct-horse-42" onDone={() => (done += 1)} />);
  await expect(page.getByTestId(ids.otpPassword)).toHaveText('correct-horse-42');
  await page.getByTestId(ids.otpDone).click();
  await expect(page.getByTestId(ids.otpReveal)).toHaveCount(0);
  await expect(page.getByTestId(ids.otpPassword)).toHaveCount(0);
  expect(done).toBe(1);
});

test('OneTimePasswordReveal_WhenCopyClicked_ShouldWriteToTheClipboard', async ({
  mount,
  page,
  context,
}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await mount(<OneTimePasswordReveal password="correct-horse-42" />);
  await page.getByTestId(ids.otpCopy).click();
  await expect(page.getByTestId(ids.otpCopy)).toHaveText('Copied');
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('correct-horse-42');
});
