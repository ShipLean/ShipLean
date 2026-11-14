import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../../AppRoutes';
import { stubShell } from '../../../../pages/screenshots/shellStub';
import {
  SPRINT_24,
  sprintsWithGoals,
  stubCapacity,
} from '../../../../pages/screenshots/fixtures/sprints';
import { touchTestIds } from '../../../../touch/testIds';
import { sprintsTestIds as ids } from '../../testIds';

// Sprint Capacity on a phone (SHIP-726, L726a). Runs in the CT phone project.

const emptyCapacity = {
  sprintId: '',
  members: [],
  teamTotal: 0,
  velocity: null,
  velocityDefault: 25,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
};
const route = `/p/SHIP/sprints/${SPRINT_24}/capacity`;

const open = async (
  page: Parameters<typeof stubShell>[0],
  mount: (c: React.JSX.Element, o: { hooksConfig: { route: string } }) => Promise<unknown>,
) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(5);
  await expect(page.getByTestId(ids.capacityCallout)).toBeVisible();
};

test('Capacity_WhenOnPhone_ShouldShowTilesAboveMemberRowsWithoutSidewaysScroll', async ({
  mount,
  page,
}) => {
  await open(page, mount);

  const tiles = [ids.capacityKpiCapacity, ids.capacityKpiCommitted, ids.capacityKpiVelocity];
  const boxes = [];
  for (const id of tiles) boxes.push((await page.getByTestId(id).boundingBox())!);
  const firstRow = (await page.getByTestId(ids.capacityRow).first().boundingBox())!;
  // One row of three tiles, all above the first member.
  expect(new Set(boxes.map((b) => Math.round(b.y))).size).toBe(1);
  expect(boxes[0]!.x).toBeLessThan(boxes[1]!.x);
  expect(boxes[1]!.x).toBeLessThan(boxes[2]!.x);
  for (const box of boxes) expect(box.y + box.height).toBeLessThan(firstRow.y);
  // Two lines per member: the days field sits under the name, the note beside it.
  const name = (await page.getByTestId(ids.capacityPoints).first().boundingBox())!;
  const days = (await page.getByTestId(ids.capacityDays).first().boundingBox())!;
  expect(days.y).toBeGreaterThan(name.y + name.height - 1);
  // Compared on the document element: a mobile context widens innerWidth to fit overflow.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  // And the table's own scroll box: rows, not a table that scrolls sideways inside its wrapper.
  const wrapper = await page
    .getByTestId(ids.capacityTable)
    .evaluate((el) => el.scrollWidth - el.clientWidth);
  expect(wrapper).toBeLessThanOrEqual(0);
  // The toolbar's own Save is not shown at touch.
  await expect(page.getByTestId(ids.capacitySave)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

test('Capacity_WhenMoreTapped_ShouldOfferClear', async ({ mount, page }) => {
  await open(page, mount);

  await page.getByTestId(ids.capacityPhoneToolbar).getByTestId(touchTestIds.moreButton).tap();

  const rows = page.getByTestId(touchTestIds.sheet).getByTestId(touchTestIds.actionSheetRow);
  await expect(rows.first()).toHaveAttribute('data-action-id', 'clear');
  await rows.first().tap();
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
});

test('Capacity_WhenCopyIsBlockedOnPhone_ShouldSayWhyInVisibleText', async ({ mount, page }) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
  await page.route('**/api/sprints/sprint-23/capacity', (r) =>
    r.fulfill({ json: { ...emptyCapacity, sprintId: 'sprint-23' } }),
  );
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(5);

  await expect(page.getByTestId(ids.capacityCopy)).toBeDisabled();
  await expect(page.getByTestId(ids.capacityCopyBlocked)).toBeVisible();
  await expect(page.getByTestId(ids.capacityCopyBlocked)).toHaveText(
    'The sprint before has no capacity entered',
  );
});

test('Capacity_WhenATeammateChangedVelocityOnPhone_ShouldShowTheWholeConflictInTheSaveBar', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.getByTestId(ids.capacityVelocity).fill('31');
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
  await expect(page.getByTestId(ids.capacityConflict)).toHaveCount(0);

  const stored = await page.evaluate(() =>
    fetch('/api/sprints/sprint-24/capacity').then((r) => r.json()),
  );
  await page.route('**/api/sprints/sprint-24/capacity', (r) =>
    r.fulfill({ json: { ...stored, velocity: 28 } }),
  );
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));

  const conflict = page.getByTestId(ids.capacityConflict);
  await expect(conflict).toHaveText('A teammate changed the velocity. Saving replaces it.');
  // Whole, not cut off: it wraps inside the bar and is not clipped by it.
  const bar = (await page.getByTestId(touchTestIds.saveBar).boundingBox())!;
  const text = (await conflict.boundingBox())!;
  expect(text.y).toBeGreaterThanOrEqual(bar.y);
  expect(text.y + text.height).toBeLessThanOrEqual(bar.y + bar.height);
  const clipped = await page
    .getByTestId(touchTestIds.saveBarMessage)
    .evaluate((el) => el.scrollWidth > el.clientWidth || el.scrollHeight > el.clientHeight);
  expect(clipped).toBe(false);
});

test('Capacity_WhenOnPhone_ShouldMatchItsScreenshot_capacity-phone', async ({ mount, page }) => {
  await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
  await open(page, mount);

  await expect(page.getByTestId(ids.capacity)).toHaveScreenshot('capacity-phone.png', {
    animations: 'disabled',
  });
});

test('Capacity_WhenDirtyOnPhone_ShouldMatchItsScreenshot_capacity-phone-dirty', async ({
  mount,
  page,
}) => {
  await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
  await open(page, mount);

  await page.getByTestId(ids.capacityDays).first().fill('6');

  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
  await expect(page).toHaveScreenshot('capacity-phone-dirty.png', { animations: 'disabled' });
});
