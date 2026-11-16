import { expect, test } from '@playwright/experimental-ct-react';
import type { SprintDto, SprintUpdate } from '@shiplean/core/api/sprints';
import { componentTestIds } from '../../../components/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { sprintsTestIds as ids } from '../testIds';
import { EditSprintRail } from './EditSprintRail';

// Edit sprint at 900px and below (SHIP-1834, SPRINT-2): "Edit {sprint}", labels above, Start and End side by side, Goal
// as a textarea, and the K5 Save bar with what changed.

const sprint: SprintDto = {
  id: 's24',
  name: 'Sprint 24',
  startDate: '2026-10-19',
  endDate: '2026-10-30',
  goal: 'Customers can check out with a saved card; passkey sign-in behind a flag.',
  status: 'current',
  rolloverStatus: null,
  datesLabel: '',
  timeLabel: '',
};

// Mutations: hideSubmit={false} at touch (Save stays in the form, no Save bar), or the stacked desktop grid (Start above
// End), or rows={2} or a Goal without min-h (one line, as .field sets the height): each fails a check below.
test('EditSprint_AtTouch_ShouldUseTheSaveBar', async ({ mount, page }) => {
  const patched: SprintUpdate[] = [];
  let done = 0;
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/sprints/s24', (route) => {
    patched.push(route.request().postDataJSON() as SprintUpdate);
    return route.fulfill({ json: sprint });
  });
  await mount(
    <EditSprintRail projectId="p1" sprint={sprint} onDone={() => done++} onDeleted={() => {}} />,
  );

  await expect(page.getByTestId(componentTestIds.dialog)).toHaveAccessibleName('Edit Sprint 24');
  await expect(page.getByTestId(ids.editSprintSubmit)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
  const start = (await page.getByTestId(ids.editSprintStart).boundingBox())!;
  const end = (await page.getByTestId(ids.editSprintEnd).boundingBox())!;
  expect(end.y, 'Start and End share a row').toBe(start.y);
  expect(end.x).toBeGreaterThan(start.x + start.width);
  await expect(page.getByTestId(ids.editSprintGoal)).toHaveAttribute('rows', '3');
  const goal = (await page.getByTestId(ids.editSprintGoal).boundingBox())!;
  expect(goal.height, 'Goal shows several lines').toBeGreaterThanOrEqual(76);

  await page.getByTestId(ids.editSprintEnd).fill('2026-11-02');

  await expect(page.getByTestId(touchTestIds.saveBarMessage)).toHaveText('End date changed');
  await expect(page.getByTestId(touchTestIds.saveBarDiscard)).toBeVisible();
  await page.getByTestId(touchTestIds.saveBarSave).tap();
  await expect
    .poll(() => patched)
    .toEqual([{ name: null, startDate: null, endDate: '2026-11-02', goal: null }]);
  await expect.poll(() => done).toBe(1);
});

// Mutation: dropping datesError from the refusal (code check) leaves the dates unmarked and the error under Goal.
test('EditSprint_AtTouch_WhenTheDatesAreRefused_ShouldSaySoUnderTheDates', async ({
  mount,
  page,
}) => {
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/sprints/s24', (route) =>
    route.fulfill({
      status: 400,
      json: { error: 'Sprints cannot overlap.', code: 'validation' },
    }),
  );
  await mount(
    <EditSprintRail projectId="p1" sprint={sprint} onDone={() => {}} onDeleted={() => {}} />,
  );

  await page.getByTestId(ids.editSprintEnd).fill('2026-11-13');
  await page.getByTestId(touchTestIds.saveBarSave).tap();

  const error = page.getByTestId(ids.editSprintError);
  await expect(error).toHaveText('Sprints cannot overlap.');
  await expect(page.getByTestId(ids.editSprintStart)).toHaveAttribute('aria-invalid', 'true');
  const errorBox = (await error.boundingBox())!;
  const endBox = (await page.getByTestId(ids.editSprintEnd).boundingBox())!;
  const goalBox = (await page.getByTestId(ids.editSprintGoal).boundingBox())!;
  expect(errorBox.y).toBeGreaterThan(endBox.y + endBox.height);
  expect(errorBox.y + errorBox.height).toBeLessThan(goalBox.y);
});
