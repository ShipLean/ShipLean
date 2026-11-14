import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../../AppRoutes';
import { stubShell } from '../../../../pages/screenshots/shellStub';
import {
  SPRINT_24,
  sprintsWithGoals,
  stubSprintBacklog,
} from '../../../../pages/screenshots/fixtures/sprints';
import { touchTestIds } from '../../../../touch/testIds';
import { sprintsTestIds as ids } from '../../testIds';

// The Sprint backlog's two-line rows and States sheet on a phone (SHIP-724, L724c). Runs in the CT phone project.

const THEMES = ['light', 'dark'] as const;
const route = `/p/SHIP/sprints/${SPRINT_24}/backlog`;

const open = async (
  page: Parameters<typeof stubShell>[0],
  mount: (c: React.JSX.Element, o: { hooksConfig: { route: string } }) => Promise<unknown>,
) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubSprintBacklog(page);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.storyRow)).toHaveCount(5);
};

test('SprintBacklogRows_WhenOnPhone_ShouldRenderTwoLineRowsWithoutSidewaysScroll', async ({
  mount,
  page,
}) => {
  await open(page, mount);

  await expect(page.locator('table')).toHaveCount(0);
  await expect(page.getByTestId(ids.taskRow)).toHaveCount(11);
  const row = page.getByTestId(ids.storyRow).first();
  // Line 1 is the title, line 2 the key, state pill and tasks done: the key sits below the title, the pill beside it.
  const title = (await row.getByRole('link').boundingBox())!;
  const key = (await row.getByText('SHIP-141').boundingBox())!;
  const pill = (await row.getByTestId(ids.stateButton).boundingBox())!;
  expect(key.y).toBeGreaterThanOrEqual(title.y + title.height - 1);
  expect(pill.y).toBeGreaterThanOrEqual(title.y + title.height - 1);
  expect(pill.height).toBeGreaterThanOrEqual(32);
  // Tasks show no points; stories do.
  await expect(row).toContainText('5');
  const task = page.getByTestId(ids.taskRow).first();
  await expect(task).not.toContainText('—');
  const widths = await page.evaluate(() => [
    document.documentElement.scrollWidth,
    document.documentElement.clientWidth,
  ]);
  expect(widths[0]).toBeLessThanOrEqual(widths[1]);
  const list = (await page.getByTestId(ids.table).boundingBox())!;
  expect(list.width).toBeLessThanOrEqual(widths[1]);
});

test('StatesSheet_WhenOpened_ShouldShowEachStatesCategory', async ({ mount, page }) => {
  await open(page, mount);

  await page.getByTestId(ids.storyRow).first().getByTestId(ids.stateButton).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('States');
  await expect(sheet.getByTestId(ids.statesSheetOption)).toHaveCount(4);
  await expect(sheet.getByTestId(ids.statesSheetCategory)).toHaveText([
    'Proposed',
    'In progress',
    'Resolved',
    'Completed',
  ]);
});

for (const theme of THEMES) {
  test(`SprintBacklogRows_WhenOpened_ShouldMatchItsScreenshot_sprint-backlog-rows-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);

    await expect(page.getByTestId(ids.table)).toHaveScreenshot(
      `sprint-backlog-rows-phone.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`StatesSheet_WhenOpened_ShouldMatchItsScreenshot_sprint-backlog-states-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);
    await page.getByTestId(ids.storyRow).first().getByTestId(ids.stateButton).tap();

    await expect(page.getByTestId(touchTestIds.sheet)).toHaveScreenshot(
      `sprint-backlog-states-phone.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
