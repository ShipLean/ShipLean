import { expect, test } from '@playwright/experimental-ct-react';
import { ConflictBanner } from './ConflictBanner';
import { componentTestIds as ids } from './testIds';

test('ConflictBanner_WhenUseMineClicked_ShouldCallBack', async ({ mount, page }) => {
  const calls: string[] = [];
  await mount(
    <ConflictBanner
      changedBy="Ana"
      onUseMine={() => calls.push('mine')}
      onKeepTheirs={() => calls.push('theirs')}
    />,
  );
  await expect(page.getByTestId(ids.conflictBanner)).toContainText('Ana');
  await page.getByTestId(ids.conflictUseMine).click();
  expect(calls).toEqual(['mine']);
});

test('ConflictBanner_WhenKeepTheirsClicked_ShouldCallBack', async ({ mount, page }) => {
  const calls: string[] = [];
  await mount(
    <ConflictBanner
      changedBy="Ana"
      onUseMine={() => calls.push('mine')}
      onKeepTheirs={() => calls.push('theirs')}
    />,
  );
  await page.getByTestId(ids.conflictKeepTheirs).click();
  expect(calls).toEqual(['theirs']);
});
