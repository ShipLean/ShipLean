import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { ChangesScreen } from '../ChangesScreen';
import { taskChangesTestIds as ids } from '../testIds';
import { fileKey } from '../types';
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

test('ChangesScreen_WhenTheOpenFileRowIsClickedAgain_ShouldScrollToItAgain', async ({
  mount,
  page,
}) => {
  const tall = (patch: string) => `@@ -0,0 +1,120 @@\n${'+x\n'.repeat(120)}${patch}`;
  const [part] = CHANGES.parts;
  const files = part.files.map((f) => ({ ...f, patch: tall('') }));
  const data = { ...CHANGES, parts: [{ ...part, files }] };
  await stubChanges(page, TASK, data, SECTION);
  await mount(screen, route);
  const target = page.locator(`[data-file-key="${fileKey(part.repo, files[1].path)}"]`);
  const row = page.getByTestId(ids.fileRow).nth(1);
  await row.click();
  await expect(target).toBeInViewport();
  // Let the diff settle, then the reader scrolls away; the same row is clicked again.
  await page.mouse.wheel(0, -20000);
  await expect(target).not.toBeInViewport();
  await row.click();
  await expect(target).toBeInViewport();
});
