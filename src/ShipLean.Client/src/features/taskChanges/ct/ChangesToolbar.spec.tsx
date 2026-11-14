import { expect, test } from '@playwright/experimental-ct-react';
import { ChangesToolbar } from '../ChangesToolbar';
import { taskChangesTestIds as ids } from '../testIds';
import { CHANGES } from './fixtures';

const toolbar = (props: Partial<Parameters<typeof ChangesToolbar>[0]> = {}) => (
  <ChangesToolbar
    data={CHANGES}
    layout="unified"
    viewedCount={1}
    fileTotal={2}
    newCommit={false}
    onReload={() => {}}
    onPickCommit={() => {}}
    onLayout={() => {}}
    {...props}
  />
);

test('ChangesToolbar_WhenACommitIsPicked_ShouldCallOnPickCommitWithItsId', async ({
  mount,
  page,
}) => {
  const picked: string[] = [];
  await mount(toolbar({ onPickCommit: (c) => picked.push(c) }));
  await page.getByTestId(ids.commitPicker).click();
  // The first option is All, then one per commit.
  await page.getByTestId(ids.commitOption).nth(2).click();
  expect(picked).toEqual([CHANGES.commits[1].id]);
  await page.getByTestId(ids.commitPicker).click();
  await page.getByTestId(ids.commitOption).first().click();
  expect(picked).toEqual([CHANGES.commits[1].id, 'all']);
});

test('ChangesToolbar_WhenLayoutIsToggled_ShouldCallOnLayout', async ({ mount, page }) => {
  const layouts: string[] = [];
  await mount(toolbar({ onLayout: (l) => layouts.push(l) }));
  await page.getByTestId(ids.layoutToggle).getByRole('button', { name: 'Split' }).click();
  expect(layouts).toEqual(['split']);
});

test('ChangesToolbar_WhenNewCommitIsSet_ShouldShowTheBannerAndReloadCallsOnReload', async ({
  mount,
  page,
}) => {
  let reloads = 0;
  await mount(toolbar({ newCommit: true, onReload: () => (reloads += 1) }));
  await expect(page.getByTestId(ids.newCommitBanner)).toBeVisible();
  await page.getByTestId(ids.newCommitReload).click();
  expect(reloads).toBe(1);
});

test('ChangesToolbar_WhenNoNewCommit_ShouldHideTheBannerAndShowViewedProgress', async ({
  mount,
  page,
}) => {
  await mount(toolbar());
  await expect(page.getByTestId(ids.newCommitBanner)).toHaveCount(0);
  await expect(page.getByTestId(ids.filesViewed)).toHaveText('1 / 2 viewed');
});

test('ChangesToolbar_WhenHalfTheFilesAreViewed_ShouldFillHalfTheTrack', async ({ mount, page }) => {
  await mount(toolbar({ viewedCount: 1, fileTotal: 2 }));
  const fill = page.getByTestId(ids.filesViewedFill);
  const track = fill.locator('..');
  const [f, t] = await Promise.all([fill.boundingBox(), track.boundingBox()]);
  expect(t?.width).toBe(90);
  expect(f?.width).toBeCloseTo(45, 0);
});
