import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { ChangesSlot } from '../../../slots/ChangesSlot';
import { taskChangesTestIds as ids } from '../testIds';
import { CHANGES, SECTION, TASK } from './fixtures';
import { stubChanges } from './stubChanges';

const route = { hooksConfig: { route: `/p/SHIP/items/${TASK.key}` } };
const slot = <ChangesSlot project={PROJECT} itemKey={TASK.key} />;

test('ChangesSlot_WhenWide_ShouldLoadTheSection', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  const reads = await stubChanges(page, TASK, CHANGES, SECTION);
  await mount(slot, route);

  await expect(page.getByTestId(ids.section)).toBeVisible();
  expect(reads.section).toBe(1);
});

test('ChangesSlot_WhenUnder900px_ShouldRenderNothingAndMakeNoRequest', async ({ mount, page }) => {
  await page.setViewportSize({ width: 900, height: 800 });
  const reads = await stubChanges(page, TASK, CHANGES, SECTION);
  await mount(slot, route);

  await expect(page.getByTestId(ids.section)).toHaveCount(0);
  await page.waitForLoadState('networkidle');
  expect(reads).toEqual({ changes: 0, section: 0 });
});
