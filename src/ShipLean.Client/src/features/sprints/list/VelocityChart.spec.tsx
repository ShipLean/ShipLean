import { expect, test } from '@playwright/experimental-ct-react';
import type { VelocityDto } from '../../../api/sprints';
import { sprintsTestIds as ids } from '../testIds';
import { VelocityCard } from './VelocityCard';

const entry = (n: number, committed: number, completed: number) => ({
  sprintId: `s${n}`,
  sprintName: `Sprint ${n}`,
  committed,
  completed,
});

const velocity = (over: Partial<VelocityDto> = {}): VelocityDto => ({
  window: 3,
  sprints: [entry(21, 18, 15), entry(22, 20, 21), entry(23, 22, 24)],
  average: 20,
  override: null,
  ...over,
});

test('VelocityCard_WhenThreeSprintsEnded_ShouldDrawCommittedAndCompletedBarsForEach', async ({
  mount,
  page,
}) => {
  await page.route('**/api/projects/p1/velocity', (route) => route.fulfill({ json: velocity() }));
  await mount(<VelocityCard projectId="p1" />);

  const bars = page.getByTestId(ids.velocityBar);
  await expect(bars).toHaveCount(6);
  const committed = page.locator('[data-kind="committed"]');
  await expect(committed.nth(0)).toHaveAttribute('data-value', '18');
  await expect(committed.nth(2)).toHaveAttribute('data-value', '22');
  const completed = page.locator('[data-kind="completed"]');
  await expect(completed.nth(1)).toHaveAttribute('data-value', '21');
  await expect(completed.nth(2)).toHaveAttribute('data-sprint', 'Sprint 23');
  await expect(page.getByTestId(ids.velocityChart)).toContainText('velocity 20 (last 3)');
  await expect(page.getByTestId(ids.velocity)).toContainText('last 3 sprints');
});

test('VelocityCard_WhenBarIsTaller_ShouldDrawItHigher', async ({ mount, page }) => {
  await page.route('**/api/projects/p1/velocity', (route) => route.fulfill({ json: velocity() }));
  await mount(<VelocityCard projectId="p1" />);

  const short = await page.locator('[data-kind="completed"]').first().boundingBox();
  const tall = await page.locator('[data-kind="completed"]').last().boundingBox();
  expect(tall!.height).toBeGreaterThan(short!.height);
});

test('VelocityCard_WhenOverrideSet_ShouldDrawTheLineAtTheOverride', async ({ mount, page }) => {
  await page.route('**/api/projects/p1/velocity', (route) =>
    route.fulfill({ json: velocity({ override: 25 }) }),
  );
  await mount(<VelocityCard projectId="p1" />);

  await expect(page.getByTestId(ids.velocityAverage)).toHaveAttribute('data-value', '25');
  await expect(page.getByTestId(ids.velocityChart)).toContainText('velocity 25 (override)');
});

test('VelocityCard_WhenNoSprintEnded_ShouldShowEmptyState', async ({ mount, page }) => {
  await page.route('**/api/projects/p1/velocity', (route) =>
    route.fulfill({ json: velocity({ sprints: [], average: 0 }) }),
  );
  await mount(<VelocityCard projectId="p1" />);

  await expect(page.getByTestId(ids.velocityEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.velocityChart)).toHaveCount(0);
});

test('VelocityCard_WhenFewerSprintsEndedThanTheWindow_ShouldLabelTheLineWithTheNumberDrawn', async ({
  mount,
  page,
}) => {
  await page.route('**/api/projects/p1/velocity', (route) =>
    route.fulfill({ json: velocity({ sprints: [entry(22, 20, 21), entry(23, 22, 24)] }) }),
  );
  await mount(<VelocityCard projectId="p1" />);

  await expect(page.getByTestId(ids.velocityChart)).toContainText('velocity 20 (last 2)');
});

test('VelocityCard_WhenDrawn_ShouldLabelTheLatestSprintsValuesDirectly', async ({
  mount,
  page,
}) => {
  await page.route('**/api/projects/p1/velocity', (route) => route.fulfill({ json: velocity() }));
  await mount(<VelocityCard projectId="p1" />);

  const labels = page.getByTestId(ids.velocityLatest);
  await expect(labels).toHaveCount(2);
  await expect(page.locator('[data-latest="committed"]')).toHaveText('22');
  await expect(page.locator('[data-latest="completed"]')).toHaveText('24');
});
