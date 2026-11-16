import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../../AppRoutes';
import { stubShell } from '../../../../pages/screenshots/shellStub';
import {
  SPRINT_24,
  sprintsWithGoals,
  stubSprintBacklog,
} from '../../../../pages/screenshots/fixtures/sprints';
import { componentTestIds } from '../../../../components/testIds';
import { touchTestIds } from '../../../../touch/testIds';
import { itemPeekTestIds } from '../../../itemPeek/testIds';
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

  const row = page.getByTestId(ids.taskRow).first();
  const resting = await row.evaluate((e) => getComputedStyle(e).backgroundColor);
  await row.getByTestId(ids.stateButton).tap();

  // The row whose sheet is open takes the accent tint.
  await expect(row).not.toHaveCSS('background-color', resting);
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(componentTestIds.stateMenuOption)).toHaveCount(4);
  await expect(sheet.getByTestId(touchTestIds.actionSheetGroup)).toHaveText([
    'Proposed',
    'In progress',
    'Resolved',
    'Completed',
  ]);
});

// SPRINT-4: the sheet names the item it changes and marks its current state (K1b).
test('SprintStatesSheet_ShouldNameTheItemAndMarkCurrent', async ({ mount, page }) => {
  await open(page, mount);

  const row = page.getByTestId(ids.taskRow).first();
  const key = (await row.getAttribute('data-key'))!;
  const title = (await row.getByTestId(itemPeekTestIds.link).first().textContent())!;
  const state = (await row.getByTestId(ids.stateButton).getAttribute('aria-label'))!.split(
    ': ',
  )[1]!;
  await row.getByTestId(ids.stateButton).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText(`State of ${key}`);
  await expect(sheet).toContainText(title);
  const options = sheet.getByTestId(componentTestIds.stateMenuOption);
  const current = options.filter({ hasText: state });
  const other = options.filter({ hasNotText: state }).first();
  await expect(current).toHaveAttribute('aria-current', 'true');
  await expect(other).not.toHaveAttribute('aria-current', 'true');
  // The current row is tinted; the others are not.
  const otherBg = await other.evaluate((e) => getComputedStyle(e).backgroundColor);
  await expect(current).not.toHaveCSS('background-color', otherBg);
});

// SHIP-1626 D3: with Tasks off a story has no ▶; the list header sums the points (SPRINT-6).
test('SprintBacklogRows_WhenTasksOff_ShouldHideTheToggle', async ({ mount, page }) => {
  await open(page, mount);
  await expect(page.getByTestId(ids.rowToggle).first()).toBeVisible();
  await expect(page.getByTestId(ids.listPoints)).toHaveText(/^5 · \d+ pts$/);
  await page.getByTestId(ids.showTasks).tap();
  await expect(page.getByTestId(ids.rowToggle)).toHaveCount(0);
});

test('TouchTaskRow_WhenUnassigned_ShouldDrawTheDashedPlaceholder', async ({ mount, page }) => {
  await open(page, mount);
  const placeholder = page.getByTestId(ids.unassignedAvatar).first();
  await expect(placeholder).toBeVisible();
  await expect(placeholder.getByTestId(componentTestIds.avatar)).toHaveCSS(
    'border-top-style',
    'dashed',
  );
});

for (const theme of THEMES) {
  test(`SprintBacklogRows_WhenOpened_ShouldMatchItsScreenshot_sprint-backlog-rows-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);
    // The sticky top bar would land mid-image in a list taller than the screen.
    await page.addStyleTag({
      content: `[data-testid="${touchTestIds.topBar}"] { display: none; }`,
    });

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
