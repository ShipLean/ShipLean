import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../../AppRoutes';
import { itemPeekTestIds } from '../../../itemPeek/testIds';
import { stubShell } from '../../../../pages/screenshots/shellStub';
import {
  SPRINT_24,
  stubCapacity,
  sprintsWithGoals,
  stubSprintBacklog,
} from '../../../../pages/screenshots/fixtures/sprints';
import { touchTestIds } from '../../../../touch/testIds';
import { sprintsTestIds as ids } from '../../testIds';

// The Sprint backlog's figures, burndown and glance rows on a phone (SHIP-724, L724b). Runs in the CT phone project.

const THEMES = ['light', 'dark'] as const;
const route = `/p/SHIP/sprints/${SPRINT_24}/backlog`;

const open = async (
  page: Parameters<typeof stubShell>[0],
  mount: (c: React.JSX.Element, o: { hooksConfig: { route: string } }) => Promise<unknown>,
) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubSprintBacklog(page);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.kpiCommitted)).toHaveText(/21/);
  await expect(page.getByTestId(ids.burndownPoint)).toHaveCount(5);
};

test('Burndown_WhenTappedOnPhone_ShouldShowThatDaysPointsAndIdeal', async ({ mount, page }) => {
  await open(page, mount);
  const readout = page.getByTestId(ids.burndownReadout);
  await expect(readout).toHaveText('Tap a day');
  const third = page.getByTestId(ids.burndownPoint).nth(2);
  const x = Number(await third.getAttribute('cx'));
  const y = Number(await third.getAttribute('cy'));

  await page.getByTestId(ids.burndown).tap({ position: { x, y } });

  await expect(readout).toHaveText('21 Oct · 19 left · ideal 16.3');
});

test('Burndown_WhenDraggedOnPhone_ShouldFollowTheFinger', async ({ mount, page }) => {
  await open(page, mount);
  const svg = page.getByTestId(ids.burndown);
  const box = (await svg.boundingBox())!;
  const dots = page.getByTestId(ids.burndownPoint);
  const lastX = Number(await dots.nth(4).getAttribute('cx'));

  await page.mouse.move(box.x + 24, box.y + 40);
  await page.mouse.down();
  await page.mouse.move(box.x + lastX, box.y + 40, { steps: 6 });
  await page.mouse.up();

  await expect(page.getByTestId(ids.burndownReadout)).toHaveText('23 Oct · 16 left · ideal 11.7');
});

test('SprintBacklog_WhenCommittedTileTapped_ShouldLinkToCapacity', async ({ mount, page }) => {
  await open(page, mount);
  await stubCapacity(page);
  const tile = page.getByTestId(ids.kpiCapacityEdit);
  await expect(tile).toHaveAttribute('href', `/p/SHIP/sprints/${SPRINT_24}/capacity`);

  await tile.tap();

  await expect(page.getByTestId(ids.capacity)).toBeVisible();
  await expect(page.getByTestId(ids.backlog)).toHaveCount(0);
});

test('SprintBacklog_WhenReadyRowTapped_ShouldListUnassignedItemsAndOpenOneInTheModal', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.getByTestId(ids.glanceReady).tap();
  const items = page.getByTestId(touchTestIds.sheet).getByTestId(ids.glanceReadyItem);
  await expect(items).toHaveText([
    /E2E test/,
    /Wire into the address form/,
    /Fallback to password/,
  ]);

  await items.first().tap();

  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(itemPeekTestIds.dialog)).toBeVisible();
});

for (const theme of THEMES) {
  test(`SprintBacklogFigures_WhenOpened_ShouldMatchItsScreenshot_sprint-backlog-figures-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);
    await page.getByTestId(ids.burndown).tap({ position: { x: 100, y: 40 } });
    await expect(page.getByTestId(ids.burndownReadout)).not.toHaveText('Tap a day');

    await expect(page.getByTestId(ids.figures)).toHaveScreenshot(
      `sprint-backlog-figures-phone.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`SprintBacklogFigures_WhenOpened_ShouldMatchItsScreenshot_sprint-backlog-glance-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);

    await expect(page.getByTestId(ids.glanceRows)).toHaveScreenshot(
      `sprint-backlog-glance-phone.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
