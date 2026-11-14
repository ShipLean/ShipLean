import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { ChangesScreen } from '../ChangesScreen';
import { taskChangesTestIds as ids } from '../testIds';
import { CHANGES, SECTION, TASK } from './fixtures';
import { stubChanges } from './stubChanges';

const route = { hooksConfig: { route: `/p/SHIP/items/${TASK.key}/changes` } };
const screen = <ChangesScreen project={PROJECT} itemKey={TASK.key} />;

test.use({ viewport: { width: 1280, height: 800 } });

test('ChangesScreen_WhenLoaded_ShouldLoadFixtureAndMountEveryColumn', async ({ mount, page }) => {
  const reads = await stubChanges(page, TASK, CHANGES, SECTION);
  await mount(screen, route);

  await expect(page.getByTestId(ids.toolbar)).toHaveText(/2 commits/);
  await expect(page.getByTestId(ids.files)).toBeVisible();
  await expect(page.getByTestId(ids.diff)).toBeVisible();
  await expect(page.getByTestId(ids.comments)).toBeVisible();
  expect(reads.changes).toBe(1);
});

test('ChangesScreen_WhenNarrow_ShouldShowTheNoticeAndMakeNoRequest', async ({ mount, page }) => {
  await page.setViewportSize({ width: 800, height: 800 });
  const reads = await stubChanges(page, TASK, CHANGES, SECTION);
  await mount(screen, route);

  await expect(page.getByTestId(ids.narrowNotice)).toBeVisible();
  await expect(page.getByTestId(ids.toolbar)).toHaveCount(0);
  expect(reads).toEqual({ changes: 0, section: 0 });
});
