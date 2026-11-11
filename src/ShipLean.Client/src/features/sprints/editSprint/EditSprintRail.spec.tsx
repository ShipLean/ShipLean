import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { SprintDto, SprintUpdate } from '@shiplean/core/api/sprints';
import { sprintsTestIds as ids } from '../testIds';
import { EditSprintRail } from './EditSprintRail';

const future: SprintDto = {
  id: 's26',
  name: 'Sprint 26',
  startDate: '2026-12-14',
  endDate: '2026-12-25',
  goal: 'Ship it',
  status: 'future',
  rolloverStatus: null,
  datesLabel: '',
  timeLabel: '',
};
const ended: SprintDto = { ...future, id: 's20', name: 'Sprint 20', status: 'past' };

async function stub(
  page: Page,
  sprintId: string,
  refusal?: { status: number; error: string; code: string },
): Promise<SprintUpdate[]> {
  const patched: SprintUpdate[] = [];
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: [] }));
  await page.route(`**/api/sprints/${sprintId}`, (route) => {
    if (refusal) return route.fulfill({ status: refusal.status, json: refusal });
    patched.push(route.request().postDataJSON() as SprintUpdate);
    return route.fulfill({ json: future });
  });
  return patched;
}

test('EditSprintRail_WhenOpened_ShouldPrefillTheSprint', async ({ mount, page }) => {
  await mount(
    <EditSprintRail projectId="p1" sprint={future} onDone={() => {}} onDeleted={() => {}} />,
  );

  await expect(page.getByTestId(ids.editSprintName)).toHaveValue('Sprint 26');
  await expect(page.getByTestId(ids.editSprintStart)).toHaveValue('2026-12-14');
  await expect(page.getByTestId(ids.editSprintEnd)).toHaveValue('2026-12-25');
  await expect(page.getByTestId(ids.editSprintGoal)).toHaveValue('Ship it');
  await expect(page.getByTestId(ids.editSprintStart)).toBeEnabled();
  await expect(page.getByTestId(ids.editSprintDatesHint)).toHaveCount(0);
});

test('EditSprintRail_WhenSprintEnded_ShouldDisableTheDatesWithAHint', async ({ mount, page }) => {
  await mount(
    <EditSprintRail projectId="p1" sprint={ended} onDone={() => {}} onDeleted={() => {}} />,
  );

  await expect(page.getByTestId(ids.editSprintStart)).toBeDisabled();
  await expect(page.getByTestId(ids.editSprintEnd)).toBeDisabled();
  await expect(page.getByTestId(ids.editSprintName)).toBeEnabled();
  await expect(page.getByTestId(ids.editSprintDatesHint)).toContainText('has ended');
});

test('EditSprintRail_WhenOnlyTheEndChanged_ShouldSendOnlyTheEndAndFinish', async ({
  mount,
  page,
}) => {
  const patched = await stub(page, 's26');
  let done = 0;
  await mount(
    <EditSprintRail
      projectId="p1"
      sprint={future}
      onDone={() => (done += 1)}
      onDeleted={() => {}}
    />,
  );

  await page.getByTestId(ids.editSprintEnd).fill('2026-12-23');
  await page.getByTestId(ids.editSprintSubmit).click();

  await expect.poll(() => done).toBe(1);
  expect(patched).toEqual([{ name: null, startDate: null, endDate: '2026-12-23', goal: null }]);
});

test('EditSprintRail_WhenNameChangedOnAnEndedSprint_ShouldSendOnlyTheName', async ({
  mount,
  page,
}) => {
  const patched = await stub(page, 's20');
  let done = 0;
  await mount(
    <EditSprintRail
      projectId="p1"
      sprint={ended}
      onDone={() => (done += 1)}
      onDeleted={() => {}}
    />,
  );

  await page.getByTestId(ids.editSprintName).fill('  Sprint 20 (retro)  ');
  await page.getByTestId(ids.editSprintSubmit).click();

  await expect.poll(() => done).toBe(1);
  expect(patched).toEqual([
    { name: 'Sprint 20 (retro)', startDate: null, endDate: null, goal: null },
  ]);
});

test('EditSprintRail_WhenNothingChanged_ShouldFinishWithoutSaving', async ({ mount, page }) => {
  const patched = await stub(page, 's26');
  let done = 0;
  await mount(
    <EditSprintRail
      projectId="p1"
      sprint={future}
      onDone={() => (done += 1)}
      onDeleted={() => {}}
    />,
  );

  await page.getByTestId(ids.editSprintSubmit).click();

  await expect.poll(() => done).toBe(1);
  expect(patched).toEqual([]);
});

test('EditSprintRail_WhenEndIsBeforeStart_ShouldRefuseWithoutSending', async ({ mount, page }) => {
  const patched = await stub(page, 's26');
  await mount(
    <EditSprintRail projectId="p1" sprint={future} onDone={() => {}} onDeleted={() => {}} />,
  );

  await page.getByTestId(ids.editSprintEnd).fill('2026-12-01');
  await page.getByTestId(ids.editSprintSubmit).click();

  await expect(page.getByTestId(ids.editSprintError)).toHaveText(
    'End must be on or after the start.',
  );
  expect(patched).toEqual([]);
  await page.getByTestId(ids.editSprintEnd).fill('2026-12-25');
  await expect(page.getByTestId(ids.editSprintError)).toHaveText('');
});

test('EditSprintRail_WhenServerRefuses_ShouldShowItBesideSaveAndStayOpen', async ({
  mount,
  page,
}) => {
  await stub(page, 's26', { status: 400, error: 'Sprints cannot overlap.', code: 'validation' });
  let done = 0;
  await mount(
    <EditSprintRail
      projectId="p1"
      sprint={future}
      onDone={() => (done += 1)}
      onDeleted={() => {}}
    />,
  );

  await page.getByTestId(ids.editSprintStart).fill('2026-12-10');
  await page.getByTestId(ids.editSprintSubmit).click();

  await expect(page.getByTestId(ids.editSprintError)).toHaveText('Sprints cannot overlap.');
  await expect(page.getByTestId(ids.editSprintStart)).toHaveValue('2026-12-10');
  expect(done).toBe(0);
});

test('EditSprintRail_WhenCancelled_ShouldFinishWithoutSaving', async ({ mount, page }) => {
  const patched = await stub(page, 's26');
  let done = 0;
  await mount(
    <EditSprintRail
      projectId="p1"
      sprint={future}
      onDone={() => (done += 1)}
      onDeleted={() => {}}
    />,
  );

  await page.getByTestId(ids.editSprintName).fill('Other');
  await page.getByTestId(ids.editSprintCancel).click();

  expect(done).toBe(1);
  expect(patched).toEqual([]);
});

test('EditSprintRail_WhenOpened_ShouldFocusTheName', async ({ mount, page }) => {
  await mount(
    <EditSprintRail projectId="p1" sprint={future} onDone={() => {}} onDeleted={() => {}} />,
  );

  await expect(page.getByTestId(ids.editSprintName)).toBeFocused();
});

test('EditSprintRail_WhenAKeyIsTypedBackAfterATeammateChangedIt_ShouldNotSendIt', async ({
  mount,
  page,
}) => {
  const patched = await stub(page, 's26');
  const rail = await mount(
    <EditSprintRail projectId="p1" sprint={future} onDone={() => {}} onDeleted={() => {}} />,
  );
  await page.getByTestId(ids.editSprintName).fill('Sprint 26b');
  await page.getByTestId(ids.editSprintGoal).fill('New goal');

  await rail.update(
    <EditSprintRail
      projectId="p1"
      sprint={{ ...future, name: 'Renamed' }}
      onDone={() => {}}
      onDeleted={() => {}}
    />,
  );
  await page.getByTestId(ids.editSprintName).fill('Sprint 26');
  await page.getByTestId(ids.editSprintSubmit).click();

  await expect.poll(() => patched.length).toBe(1);
  expect(patched[0]).toEqual({ name: null, startDate: null, endDate: null, goal: 'New goal' });
});

async function stubDelete(page: Page, refusal?: { error: string; code: string }) {
  const deleted: string[] = [];
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/sprints/s26', (route) => {
    if (route.request().method() !== 'DELETE') return route.fallback();
    if (refusal) return route.fulfill({ status: 409, json: refusal });
    deleted.push(route.request().url());
    return route.fulfill({ status: 204 });
  });
  return deleted;
}

test('EditSprintRail_WhenDeleteConfirmed_ShouldDeleteTheSprintAndReportIt', async ({
  mount,
  page,
}) => {
  const deleted = await stubDelete(page);
  let gone = 0;
  await mount(
    <EditSprintRail
      projectId="p1"
      sprint={future}
      onDone={() => {}}
      onDeleted={() => (gone += 1)}
    />,
  );

  await page.getByTestId(ids.deleteSprint).click();
  await expect(page.getByTestId(ids.editSprint)).toContainText(
    'Delete Sprint 26? Only a sprint with no work items can be deleted.',
  );
  await expect(page.getByTestId(ids.deleteSprintCancel)).toBeFocused();
  await page.getByTestId(ids.deleteSprintConfirm).click();

  await expect.poll(() => gone).toBe(1);
  expect(deleted).toHaveLength(1);
});

test('EditSprintRail_WhenDeleteCancelled_ShouldSendNothingAndRefocusDelete', async ({
  mount,
  page,
}) => {
  const deleted = await stubDelete(page);
  await mount(
    <EditSprintRail projectId="p1" sprint={future} onDone={() => {}} onDeleted={() => {}} />,
  );

  await page.getByTestId(ids.deleteSprint).click();
  await page.getByTestId(ids.deleteSprintCancel).click();

  await expect(page.getByTestId(ids.deleteSprintConfirm)).toHaveCount(0);
  await expect(page.getByTestId(ids.deleteSprint)).toBeFocused();
  expect(deleted).toEqual([]);
});

test('EditSprintRail_WhenSprintHasWorkItems_ShouldShowTheRefusalAndStay', async ({
  mount,
  page,
}) => {
  await stubDelete(page, {
    error:
      "Sprint 26 has work items, so it can't be deleted. Move them to another sprint or the backlog first.",
    code: 'conflict',
  });
  let gone = 0;
  await mount(
    <EditSprintRail
      projectId="p1"
      sprint={future}
      onDone={() => {}}
      onDeleted={() => (gone += 1)}
    />,
  );

  await page.getByTestId(ids.deleteSprint).click();
  await page.getByTestId(ids.deleteSprintConfirm).click();

  await expect(page.getByTestId(ids.deleteSprintError)).toHaveText(
    "Sprint 26 has work items, so it can't be deleted. Move them to another sprint or the backlog first.",
  );
  await expect(page.getByTestId(ids.deleteSprintConfirm)).toBeEnabled();
  expect(gone).toBe(0);
});
