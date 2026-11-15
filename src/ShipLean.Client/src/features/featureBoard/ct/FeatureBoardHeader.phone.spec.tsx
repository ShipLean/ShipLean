import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { taskboardTestIds as taskboardIds } from '../../taskboard/testIds';
import { FeatureBoardSlot } from '../../../slots/FeatureBoardSlot';
import { featureBoardTestIds as ids } from '../testIds';
import { PARENT_KEY, PARENT_VIEW } from './fixtures';
import { stubFeatureBoard } from './stubFeatureBoard';

// The Feature board's header on a phone (SHIP-722 D10). Runs in the CT phone project.

const route = { hooksConfig: { route: `/p/SHIP/items/${PARENT_KEY}/board` } };
const THEMES = ['light', 'dark'] as const;

test('FeatureBoardHeader_WhenOnPhone_ShouldDropBackLinkAndWrapJumpTo', async ({ mount, page }) => {
  await stubFeatureBoard(page, PARENT_VIEW);
  await mount(<FeatureBoardSlot project={PROJECT} itemKey={PARENT_KEY} />, route);

  const header = page.getByTestId(ids.header);
  await expect(header).toBeVisible();
  await expect(page.getByTestId(ids.back)).toBeHidden();
  await expect(header.getByText('Feature board', { exact: true })).toBeHidden();
  const key = (await page.getByTestId(ids.key).boundingBox())!;
  const title = (await page.getByTestId(ids.title).boundingBox())!;
  expect(title.y).toBeGreaterThan(key.y + key.height - 1);
  const bar = (await page.getByTestId(ids.pointsBar).boundingBox())!;
  expect(bar.width).toBeGreaterThan(150);
  const chips = page.getByTestId(ids.jumpChip);
  const count = await chips.count();
  expect(count).toBeGreaterThan(1);
  for (let i = 0; i < count; i++) {
    expect((await chips.nth(i).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
  const pills = page.getByTestId(ids.sprints).getByTestId(taskboardIds.rowSprint);
  for (let i = 0; i < (await pills.count()); i++) {
    expect((await pills.nth(i).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
  const width = await page.evaluate(() => [
    document.documentElement.scrollWidth,
    document.documentElement.clientWidth,
  ]);
  expect(width[0]).toBeLessThanOrEqual(width[1]);
});

for (const theme of THEMES) {
  test(`FeatureBoardHeader_WhenOnPhone_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubFeatureBoard(page, PARENT_VIEW);
    await mount(<FeatureBoardSlot project={PROJECT} itemKey={PARENT_KEY} />, route);
    await expect(page.getByTestId(ids.header)).toHaveScreenshot(
      `feature-board-header-phone.${theme}.png`,
    );
  });
}
