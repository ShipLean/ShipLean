import { expect, test } from '@playwright/experimental-ct-react';
import { stubRetros } from '../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../testIds';
import { RetroActionDialogHarness } from './ct/RetroActionDialogHarness';

test('RetroActionDialog_WhenOpened_ShouldFetchWithThePointIds', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await expect(page.getByTestId(ids.retroActionDialog)).toHaveCount(0);
  await page.getByRole('button', { name: 'open' }).click();
  await expect(page.getByTestId(ids.retroActionDialog)).toBeVisible();
  await expect(page.getByTestId(ids.retroActionSave)).toBeVisible();
  await expect.poll(() => stub.reads('/retros/action-dialog').length).toBe(1);
  const query = new URLSearchParams(stub.reads('/retros/action-dialog')[0].query);
  expect(query.getAll('point')).toEqual(['pt-1', 'pt-2']);
  expect(query.get('item')).toBe('item-1');
});
