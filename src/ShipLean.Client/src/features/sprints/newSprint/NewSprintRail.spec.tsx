import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { SprintCreate } from '../../../api/sprints';
import { sprintsTestIds as ids } from '../testIds';
import { NewSprintRail } from './NewSprintRail';

const sprint27: SprintCreate = {
  name: 'Sprint 27',
  startDate: '2026-11-30',
  endDate: '2026-12-11',
  goal: '',
};
const sprint28: SprintCreate = {
  name: 'Sprint 28',
  startDate: '2026-12-14',
  endDate: '2026-12-25',
  goal: '',
};

interface Posted {
  bodies: SprintCreate[];
}

// GET next answers the first defaults until a create has happened, then the ones that follow it.
async function stub(
  page: Page,
  refusal?: { status: number; error: string; code: string },
): Promise<Posted> {
  const posted: Posted = { bodies: [] };
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill({ json: posted.bodies.length === 0 ? sprint27 : sprint28 }),
  );
  await page.route('**/api/projects/p1/sprints', (route) => {
    if (route.request().method() !== 'POST') return route.fulfill({ json: [] });
    if (refusal) return route.fulfill({ status: refusal.status, json: refusal });
    posted.bodies.push(route.request().postDataJSON() as SprintCreate);
    return route.fulfill({
      json: {
        id: 's27',
        name: 'Sprint 27',
        startDate: sprint27.startDate,
        endDate: sprint27.endDate,
        goal: '',
        status: 'future',
        rolloverStatus: null,
        datesLabel: '',
        timeLabel: '',
      },
    });
  });
  return posted;
}

test('NewSprintRail_WhenLoaded_ShouldPrefillTheServersDefaults', async ({ mount, page }) => {
  await stub(page);
  await mount(<NewSprintRail projectId="p1" />);

  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');
  await expect(page.getByTestId(ids.newSprintStart)).toHaveValue('2026-11-30');
  await expect(page.getByTestId(ids.newSprintEnd)).toHaveValue('2026-12-11');
  await expect(page.getByTestId(ids.newSprintGoal)).toHaveValue('');
});

test('NewSprintRail_WhenEditedAndCreated_ShouldPostWhatWasTypedThenShowTheNextDefaults', async ({
  mount,
  page,
}) => {
  const posted = await stub(page);
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintName).fill('  Hardening  ');
  await page.getByTestId(ids.newSprintGoal).fill('Fix the flaky checkout');
  await page.getByTestId(ids.newSprintEnd).fill('2026-12-09');
  await page.getByTestId(ids.newSprintSubmit).click();

  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 28');
  expect(posted.bodies).toEqual([
    {
      name: 'Hardening',
      startDate: '2026-11-30',
      endDate: '2026-12-09',
      goal: 'Fix the flaky checkout',
    },
  ]);
  await expect(page.getByTestId(ids.newSprintStart)).toHaveValue('2026-12-14');
  await expect(page.getByTestId(ids.newSprintGoal)).toHaveValue('');
});

test('NewSprintRail_WhenServerRefusesOverlap_ShouldShowItInlineAndKeepTheForm', async ({
  mount,
  page,
}) => {
  await stub(page, { status: 400, error: 'Sprints cannot overlap.', code: 'validation' });
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintStart).fill('2026-11-16');
  await page.getByTestId(ids.newSprintSubmit).click();

  await expect(page.getByTestId(ids.newSprintError)).toHaveText('Sprints cannot overlap.');
  await expect(page.getByTestId(ids.newSprintStart)).toHaveValue('2026-11-16');

  await page.getByTestId(ids.newSprintStart).fill('2026-11-30');
  await expect(page.getByTestId(ids.newSprintError)).toHaveText('');
});

test('NewSprintRail_WhenDefaultsFail_ShouldShowAnErrorInsteadOfLoadingForever', async ({
  mount,
  page,
}) => {
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill({
      status: 500,
      json: { error: 'Something went wrong.', code: 'internal_error' },
    }),
  );
  await mount(<NewSprintRail projectId="p1" />);

  await expect(page.getByTestId(ids.newSprintLoadError)).toHaveText(
    'The defaults could not be loaded.',
  );
});

test('NewSprintRail_WhenEndIsBeforeStart_ShouldRefuseWithoutPosting', async ({ mount, page }) => {
  const posted = await stub(page);
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintEnd).fill('2026-11-20');
  await page.getByTestId(ids.newSprintSubmit).click();

  await expect(page.getByTestId(ids.newSprintError)).toHaveText(
    'End must be on or after the start.',
  );
  expect(posted.bodies).toEqual([]);
  await page.getByTestId(ids.newSprintEnd).fill('2026-12-11');
  await expect(page.getByTestId(ids.newSprintError)).toHaveText('');
});

test('NewSprintRail_WhenADateIsCleared_ShouldRefuseWithoutPosting', async ({ mount, page }) => {
  const posted = await stub(page);
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintStart).fill('');
  await page.getByTestId(ids.newSprintSubmit).click();

  await expect(page.getByTestId(ids.newSprintError)).toHaveText(
    'End must be on or after the start.',
  );
  expect(posted.bodies).toEqual([]);
});

test('NewSprintRail_WhenDefaultsRefetchAfterAnEdit_ShouldKeepWhatThePersonHas', async ({
  mount,
  page,
}) => {
  let served: SprintCreate = sprint27;
  let reads = 0;
  await page.route('**/api/projects/p1/sprints/next', (route) => {
    reads += 1;
    return route.fulfill({ json: served });
  });
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: [] }));
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintStart).fill('2026-12-01');
  served = sprint28;
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
  await expect.poll(() => reads).toBe(2);

  await expect(page.getByTestId(ids.newSprintStart)).toHaveValue('2026-12-01');
  await expect(page.getByTestId(ids.newSprintEnd)).toHaveValue('2026-12-11');
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');
});

// GET next answers the defaults, or the name for the range it is asked about.
async function stubNamed(page: Page, names: Record<string, string>): Promise<string[]> {
  const asked: string[] = [];
  await page.route('**/api/projects/p1/sprints/next**', (route) => {
    const url = new URL(route.request().url());
    const start = url.searchParams.get('start');
    if (start === null) return route.fulfill({ json: sprint27 });
    asked.push(`${start}..${url.searchParams.get('end')}`);
    return route.fulfill({
      json: { ...sprint27, name: names[start] ?? 'Unknown', startDate: start },
    });
  });
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: [] }));
  return asked;
}

test('NewSprintRail_WhenStartChangesAndNameUntouched_ShouldReplaceTheSuggestedName', async ({
  mount,
  page,
}) => {
  const asked = await stubNamed(page, { '2027-01-04': '2027 Q1 Sprint 1' });
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintEnd).fill('2027-01-15');
  await page.getByTestId(ids.newSprintStart).fill('2027-01-04');

  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('2027 Q1 Sprint 1');
  expect(asked).toContain('2027-01-04..2027-01-15');
});

test('NewSprintRail_WhenNameTyped_ShouldKeepItThroughDateChanges', async ({ mount, page }) => {
  const asked = await stubNamed(page, { '2027-01-04': '2027 Q1 Sprint 1' });
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintName).fill('Hardening');
  await page.getByTestId(ids.newSprintStart).fill('2027-01-04');

  await expect(page.getByTestId(ids.newSprintStart)).toHaveValue('2027-01-04');
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Hardening');
  expect(asked).toEqual([]);
});

test('NewSprintRail_WhenTheSuggestedNameIsStillLoading_ShouldKeepCreateOffUntilItArrives', async ({
  mount,
  page,
}) => {
  const posted: SprintCreate[] = [];
  let release = () => {};
  const held = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/projects/p1/sprints/next**', async (route) => {
    const start = new URL(route.request().url()).searchParams.get('start');
    if (start === null) return route.fulfill({ json: sprint27 });
    await held;
    return route.fulfill({ json: { ...sprint27, name: '2027 Q1 Sprint 1', startDate: start } });
  });
  await page.route('**/api/projects/p1/sprints', (route) => {
    if (route.request().method() === 'POST') posted.push(route.request().postDataJSON());
    return route.fulfill({ json: [] });
  });
  await mount(<NewSprintRail projectId="p1" />);
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');

  await page.getByTestId(ids.newSprintStart).fill('2027-01-04');
  await page.getByTestId(ids.newSprintEnd).fill('2027-01-15');
  await expect(page.getByTestId(ids.newSprintSubmit)).toBeDisabled();

  release();
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('2027 Q1 Sprint 1');
  await expect(page.getByTestId(ids.newSprintSubmit)).toBeEnabled();
  await page.getByTestId(ids.newSprintSubmit).click();
  await expect.poll(() => posted.length).toBe(1);
  expect(posted[0]).toMatchObject({ name: '2027 Q1 Sprint 1', startDate: '2027-01-04' });
});
