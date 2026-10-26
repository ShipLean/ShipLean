import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { sprintsTestIds as ids } from '../testIds';
import { SprintGoalEditor } from './SprintGoalEditor';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 's24', name: 'Sprint 24', goal: 'Old goal' } as SprintDto;

test('SprintGoalEditor_WhenSaved_ShouldPatchOnlyTheGoalAndFinish', async ({ mount, page }) => {
  let body: unknown;
  await page.route('**/api/sprints/s24', async (route) => {
    body = route.request().postDataJSON();
    await route.fulfill({ json: { ...sprint, goal: 'New goal' } });
  });
  let done = 0;
  await mount(<SprintGoalEditor project={project} sprint={sprint} onDone={() => (done += 1)} />);
  await expect(page.getByTestId(ids.goalInput)).toHaveValue('Old goal');

  await page.getByTestId(ids.goalInput).fill('New goal');
  await page.getByTestId(ids.goalSave).click();

  await expect.poll(() => done).toBe(1);
  expect(body).toEqual({ name: null, startDate: null, endDate: null, goal: 'New goal' });
});

test('SprintGoalEditor_WhenCancelledOrEscaped_ShouldFinishWithoutSaving', async ({
  mount,
  page,
}) => {
  let saves = 0;
  await page.route('**/api/sprints/s24', (route) => {
    saves += 1;
    return route.fulfill({ json: sprint });
  });
  let done = 0;
  await mount(<SprintGoalEditor project={project} sprint={sprint} onDone={() => (done += 1)} />);

  await page.getByTestId(ids.goalInput).fill('Changed');
  await page.getByTestId(ids.goalCancel).click();
  await expect.poll(() => done).toBe(1);
  await page.getByTestId(ids.goalInput).press('Escape');
  await expect.poll(() => done).toBe(2);
  expect(saves).toBe(0);
});

test('SprintGoalEditor_WhenSaveFails_ShouldShowTheErrorAndStayOpen', async ({ mount, page }) => {
  await page.route('**/api/sprints/s24', (route) =>
    route.fulfill({ status: 400, json: { error: 'Goal is too long.', code: 'validation' } }),
  );
  let done = 0;
  await mount(<SprintGoalEditor project={project} sprint={sprint} onDone={() => (done += 1)} />);

  await page.getByTestId(ids.goalSave).click();

  await expect(page.getByTestId(ids.goalError)).toHaveText('Goal is too long.');
  expect(done).toBe(0);
});
