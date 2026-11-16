import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { sprintsTestIds as ids } from '../testIds';
import { NewSprintRail } from './NewSprintRail';

// New sprint at 900px and below (SHIP-1834, SHIP-723 P5): labels above, Start and End side by side, Goal as a
// textarea, and a refused date range said under the dates with the dates marked.

// Mutations: the stacked desktop grid (Start above End) fails the shared-row check; dropping datesError for a
// validation refusal leaves the dates unmarked and the error under Goal.
test('NewSprint_AtTouch_ShouldShowAnOverlapUnderTheSideBySideDates', async ({ mount, page }) => {
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill({
      json: { name: 'Sprint 27', startDate: '2026-11-30', endDate: '2026-12-11', goal: '' },
    }),
  );
  await page.route('**/api/projects/p1/sprints', (route) =>
    route.request().method() === 'POST'
      ? route.fulfill({
          status: 400,
          json: { error: 'Sprints cannot overlap.', code: 'validation' },
        })
      : route.fulfill({ json: [] }),
  );
  await mount(<NewSprintRail projectId="p1" onClose={() => {}} />);

  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');
  const start = (await page.getByTestId(ids.newSprintStart).boundingBox())!;
  const end = (await page.getByTestId(ids.newSprintEnd).boundingBox())!;
  expect(end.y, 'Start and End share a row').toBe(start.y);
  await expect(page.getByTestId(ids.newSprintGoal)).toHaveAttribute('rows', '3');
  await expect(page.getByTestId(ids.newSprintError)).toBeHidden();

  await page.getByTestId(touchTestIds.dialogPrimary).getByTestId(ids.newSprintCreate).tap();

  const error = page.getByTestId(ids.newSprintError);
  await expect(error).toHaveText('Sprints cannot overlap.');
  await expect(page.getByTestId(ids.newSprintStart)).toHaveAttribute('aria-invalid', 'true');
  const errorBox = (await error.boundingBox())!;
  const goalBox = (await page.getByTestId(ids.newSprintGoal).boundingBox())!;
  expect(errorBox.y).toBeGreaterThan(end.y + end.height);
  expect(errorBox.y + errorBox.height).toBeLessThan(goalBox.y);
});
