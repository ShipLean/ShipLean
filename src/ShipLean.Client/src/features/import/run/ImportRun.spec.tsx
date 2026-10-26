import { expect, test } from '@playwright/experimental-ct-react';
import { ProjectJobs } from '../jobs/ProjectJobs';
import {
  connection,
  importJob,
  json,
  mappingDto,
  mappingWithNewStatus,
  project,
  stubImport,
  stubJob,
  stubMapping,
} from '../ct/fixtures';
import { ImportStepper } from '../stepper/ImportStepper';
import { importTestIds as ids } from '../testIds';

async function stubWorkingConnection(page: Parameters<typeof stubImport>[0]) {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, { ok: true, who: { displayName: 'Priya Shah', email: null }, message: null }),
  );
}

test('ImportStepper_WhenJobInTheUrl_ShouldOpenThatJobOnStepFive', async ({ mount, page }) => {
  await stubImport(page, connection);
  await stubJob(page, importJob({ status: 'done' }));
  await mount(<ImportStepper project={project} />, {
    hooksConfig: { route: '/p/SHIP/import?job=job1' },
  });
  await expect(page.getByTestId(ids.runProgress)).toContainText('PLATFORM → SHIP');
  await expect(page.getByTestId(ids.runChip)).toHaveText('Done');
  await expect(page.getByTestId(ids.step5)).toHaveAttribute('aria-current', 'step');
});

test('ImportStepper_WhenStartedFromStepFive_ShouldSendTheEditedMappingAndShowTheJob', async ({
  mount,
  page,
}) => {
  await stubWorkingConnection(page);
  await stubMapping(page);
  const bodies: { mapping: typeof mappingDto }[] = [];
  await page.route('**/api/import/jobs', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ status: 'queued' }));
  });
  await stubJob(page, importJob({ status: 'queued' }));
  await mount(<ImportStepper project={project} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.projectRow).filter({ hasText: 'PLATFORM' }).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.mappingIncludeComponents).uncheck();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.runStart).click();
  await expect(page.getByTestId(ids.runChip)).toHaveText('Queued');
  expect(bodies).toHaveLength(1);
  expect(bodies[0].mapping.fields.includeComponents).toBe(false);
});

test('ProjectJobs_WhenRerun_ShouldOpenTheNewRunInTheStepper', async ({ mount, page }) => {
  await stubImport(page, connection);
  const reads = await stubMapping(page);
  const done = importJob({ id: 'old', status: 'done', finishedLabel: '2 days ago' });
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) => json(route, [done]));
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/old/rerun', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ id: 'old', status: 'queued' }));
  });
  await stubJob(page, importJob({ id: 'old', status: 'queued' }));
  await mount(
    <>
      <ImportStepper project={project} />
      <ProjectJobs project={project} />
    </>,
  );
  await expect(page.getByTestId(ids.runJobsRow)).toContainText('PLATFORM → SHIP');
  await expect(page.getByTestId(ids.runJobsOpen)).toHaveAttribute('href', '/p/SHIP/import?job=old');
  await page.getByTestId(ids.runJobsRerun).click();
  await expect(page.getByTestId(ids.runChip)).toHaveText('Queued');
  expect(bodies).toEqual([{ mapping: null }]);
  expect(reads).toEqual([{ connectionId: 'c1', jiraProjectKey: 'PLATFORM', previousJobId: 'old' }]);
});

const mobileJob = (overrides = {}) =>
  importJob({
    id: 'old',
    jiraProjectKey: 'MOBILE',
    projectKey: 'MOB',
    status: 'done',
    finishedLabel: '2 days ago',
    ...overrides,
  });
const mobProject = { ...project, key: 'MOB', nextItemNumber: 9 };

test('ProjectJobs_WhenTheMappingHasANewStatus_ShouldOpenMappingAndNotRerun', async ({
  mount,
  page,
}) => {
  await stubImport(page, connection);
  await stubMapping(page, mappingWithNewStatus);
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) =>
    json(route, [mobileJob()]),
  );
  const posted: string[] = [];
  await page.route('**/api/import/jobs/old/rerun', (route) => {
    posted.push(route.request().url());
    return json(route, mobileJob({ status: 'queued' }));
  });
  await mount(
    <>
      <ImportStepper project={mobProject} />
      <ProjectJobs project={mobProject} />
    </>,
    { hooksConfig: { route: '/p/MOB/import' } },
  );
  await page.getByTestId(ids.runJobsRerun).click();
  await expect(page.getByTestId(ids.mappingStep)).toBeVisible();
  await expect(page.getByTestId(ids.mappingNewChip)).toHaveCount(1);
  expect(posted).toEqual([]);
});

test('JobProgress_WhenTheMappingHasANewStatus_ShouldOpenMappingAndNotRerun', async ({
  mount,
  page,
}) => {
  await stubImport(page, connection);
  await stubMapping(page, mappingWithNewStatus);
  await stubJob(page, mobileJob());
  const posted: string[] = [];
  await page.route('**/api/import/jobs/old/rerun', (route) => {
    posted.push(route.request().url());
    return json(route, mobileJob({ status: 'queued' }));
  });
  await mount(<ImportStepper project={mobProject} />, {
    hooksConfig: { route: '/p/MOB/import?job=old' },
  });
  await page.getByTestId(ids.runRerun).click();
  await expect(page.getByTestId(ids.mappingStep)).toBeVisible();
  await expect(page.getByTestId(ids.mappingNewChip)).toHaveCount(1);
  expect(posted).toEqual([]);
});

test('JobProgress_WhenNothingIsNew_ShouldRerunWithTheSavedMapping', async ({ mount, page }) => {
  await stubImport(page, connection);
  await stubMapping(page);
  await stubJob(page, mobileJob());
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/old/rerun', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, mobileJob({ status: 'queued' }));
  });
  await mount(<ImportStepper project={mobProject} />, {
    hooksConfig: { route: '/p/MOB/import?job=old' },
  });
  await page.getByTestId(ids.runRerun).click();
  await expect.poll(() => bodies).toEqual([{ mapping: null }]);
});

test('JobProgress_WhenTheMappingCannotBeRead_ShouldShowWhy', async ({ mount, page }) => {
  await stubImport(page, connection);
  await page.route('**/api/import/mapping', (route) =>
    json(route, { error: 'Jira is not reachable.', code: 'validation' }, 400),
  );
  await stubJob(page, mobileJob());
  await mount(<ImportStepper project={mobProject} />, {
    hooksConfig: { route: '/p/MOB/import?job=old' },
  });
  await page.getByTestId(ids.runRerun).click();
  await expect(page.getByTestId(ids.runStopError)).toHaveText('Jira is not reachable.');
});

test('ProjectJobs_WhenListed_ShouldNameTheStatusInWords', async ({ mount, page }) => {
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) =>
    json(route, [importJob({ id: 'old', status: 'doneWithErrors' })]),
  );
  await mount(<ProjectJobs project={project} />);
  await expect(page.getByTestId(ids.runJobsStatus)).toHaveText('Done with errors');
});

test('JobOutcome_WhenAFinishedJobIsOpened_ShouldLinkToTheBacklog', async ({ mount, page }) => {
  await stubImport(page, connection);
  await stubJob(page, importJob({ status: 'done' }));
  await mount(<ImportStepper project={project} />, {
    hooksConfig: { route: '/p/SHIP/import?job=job1' },
  });
  await expect(page.getByTestId(ids.runOutcome)).toContainText('Import finished');
  await expect(page.getByTestId(ids.runOutcome).getByRole('link')).toHaveText('Open SHIP backlog');
});

test('JobOutcome_WhenARerunFinished_ShouldShowWhatItTouched', async ({ mount, page }) => {
  const base = importJob({ status: 'done' });
  await stubImport(page, connection);
  await stubJob(
    page,
    importJob({
      status: 'done',
      progress: {
        ...base.progress,
        rerun: true,
        rerunResult: { updated: 37, unchanged: 1163, added: 2 },
      },
    }),
  );
  await mount(<ImportStepper project={project} />, {
    hooksConfig: { route: '/p/SHIP/import?job=job1' },
  });
  await expect(page.getByTestId(ids.runOutcome)).toContainText('Re-run finished');
  await expect(page.getByTestId(ids.runRerunResult)).toHaveText(
    '37 updated · 1,163 unchanged · 2 added',
  );
});

test('ProjectJobs_WhenAJobIsRunning_ShouldNotOfferRerun', async ({ mount, page }) => {
  const running = importJob({ id: 'live' });
  const done = importJob({ id: 'old', status: 'done' });
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) =>
    json(route, [running, done]),
  );
  await mount(<ProjectJobs project={project} />);
  await expect(page.getByTestId(ids.runJobsRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.runJobsRerun).first()).toBeDisabled();
  await expect(page.getByTestId(ids.runJobsRerun).last()).toBeDisabled();
});

test('ProjectJobs_WhenRerunIsRefused_ShouldShowTheServersMessage', async ({ mount, page }) => {
  await stubImport(page, connection);
  await stubMapping(page);
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) =>
    json(route, [importJob({ id: 'old', status: 'done' })]),
  );
  await page.route('**/api/import/jobs/old/rerun', (route) =>
    json(
      route,
      { code: 'conflict', error: 'SHIP already has work items from another import.' },
      409,
    ),
  );
  await mount(<ProjectJobs project={project} />);
  await page.getByTestId(ids.runJobsRerun).click();
  await expect(page.getByTestId(ids.runJobsNote)).toContainText('already has work items');
});

test('ProjectJobs_WhenNoJobs_ShouldSayNoImportsYet', async ({ mount, page }) => {
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) => json(route, []));
  await mount(<ProjectJobs project={project} />);
  await expect(page.getByTestId(ids.runJobsEmpty)).toBeVisible();
});

test('ProjectJobs_WhenNotAdmin_ShouldRenderNothing', async ({ mount, page }) => {
  const asked: string[] = [];
  await page.route('**/api/**', (route) => {
    asked.push(route.request().url());
    return json(route, [], 403);
  });
  await mount(<ProjectJobs project={{ ...project, role: 'member' }} />);
  await expect(page.getByTestId(ids.runJobs)).toHaveCount(0);
  expect(asked).toEqual([]);
});
