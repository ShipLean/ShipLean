import { expect, test } from '@playwright/experimental-ct-react';
import {
  connection,
  failedRow,
  importJob,
  jiraProjects,
  json,
  mappingDto,
  project,
  stubJob,
} from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';
import { RunStep } from './RunStep';

const platform = jiraProjects[0];
const mobile = jiraProjects[1];
const unused = () => {};

const readyProps = {
  project,
  connectionId: connection.id,
  jira: platform,
  mapping: mappingDto,
  rerunMapping: mappingDto,
  jobId: null,
  onBack: unused,
};

test('RunStep_WhenStarted_ShouldPostTheStepperChoicesAndOpenTheJob', async ({ mount, page }) => {
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ status: 'queued' }));
  });
  const opened: string[] = [];
  await mount(<RunStep {...readyProps} onJob={(id) => opened.push(id)} />);
  await expect(page.getByTestId(ids.runTotals)).toContainText('1,200 issues');
  await page.getByTestId(ids.runStart).click();
  await expect.poll(() => opened).toEqual(['job1']);
  expect(bodies).toEqual([
    {
      connectionId: 'c1',
      jiraProjectKey: 'PLATFORM',
      target: { kind: 'existingProject', key: null, name: null, projectId: project.id },
      mapping: mappingDto,
    },
  ]);
});

test('RunStep_WhenReady_ShouldListTheNewUsersAndStates', async ({ mount, page }) => {
  await mount(<RunStep {...readyProps} onJob={unused} />);
  await expect(page.getByTestId(ids.runReadyNew)).toHaveText('1 new users · 1 new states');
});

test('RunStep_WhenStartIsRefused_ShouldShowTheServersMessage', async ({ mount, page }) => {
  await page.route('**/api/import/jobs', (route) =>
    json(route, { code: 'conflict', error: 'SHIP already has work items.' }, 409),
  );
  const opened: string[] = [];
  await mount(<RunStep {...readyProps} onJob={(id) => opened.push(id)} />);
  await page.getByTestId(ids.runStart).click();
  await expect(page.getByTestId(ids.runStartError)).toHaveText('SHIP already has work items.');
  expect(opened).toEqual([]);
});

test('RunStep_WhenRerun_ShouldPostTheMappingToThePreviousJob', async ({ mount, page }) => {
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/j1/rerun', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ id: 'j1', status: 'queued' }));
  });
  const opened: string[] = [];
  await mount(<RunStep {...readyProps} jira={mobile} onJob={(id) => opened.push(id)} />);
  await expect(page.getByTestId(ids.runStart)).toHaveCount(0);
  await page.getByTestId(ids.runRerun).click();
  await expect.poll(() => opened).toEqual(['j1']);
  expect(bodies).toEqual([{ mapping: mappingDto }]);
});

test('RunStep_WhenMappingWasSkipped_ShouldRerunWithNoMapping', async ({ mount, page }) => {
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/j1/rerun', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ id: 'j1', status: 'queued' }));
  });
  await mount(<RunStep {...readyProps} jira={mobile} rerunMapping={null} onJob={unused} />);
  await page.getByTestId(ids.runRerun).click();
  await expect.poll(() => bodies).toEqual([{ mapping: null }]);
});

test('RunStep_WhenRerunIsRefused_ShouldShowTheServersMessage', async ({ mount, page }) => {
  await page.route('**/api/import/jobs/j1/rerun', (route) =>
    json(route, { code: 'conflict', error: 'A job for PLATFORM is already running.' }, 409),
  );
  await mount(<RunStep {...readyProps} jira={mobile} onJob={unused} />);
  await page.getByTestId(ids.runRerun).click();
  await expect(page.getByTestId(ids.runStartError)).toContainText('already running');
});

const jobProps = { ...readyProps, jobId: 'job1', onJob: unused };

test('RunStep_WhenJobHasNoProjectYet_ShouldPollUntilItHasOne', async ({ mount, page }) => {
  let created = false;
  await stubJob(page, () =>
    created
      ? importJob()
      : importJob({
          projectId: null,
          progress: {
            ...importJob().progress,
            percent: 3,
            message: 'Reading issues from Jira (200 so far)',
          },
        }),
  );
  await mount(<RunStep {...jobProps} />);
  await expect(page.getByTestId(ids.runMessage)).toContainText('200 so far');
  await expect(page.getByTestId(ids.runPercent)).toHaveText('3%');
  // Nothing but the poll can bring the next answer in.
  created = true;
  await expect(page.getByTestId(ids.runPercent)).toHaveText('40%');
  await expect(page.getByTestId(ids.runMessage)).toHaveText('Issues');
});

test('RunStep_WhenJobRuns_ShouldShowCountsAndLogAndStopIt', async ({ mount, page }) => {
  await stubJob(page, importJob());
  let stopped = 0;
  await page.route('**/api/import/jobs/job1/stop', (route) => {
    stopped += 1;
    return json(route, importJob());
  });
  await mount(<RunStep {...jobProps} />);
  await expect(page.getByTestId(ids.runCount).filter({ hasText: 'Issues' })).toContainText(
    '480 / 1,200',
  );
  await expect(page.getByTestId(ids.runLogLine)).toHaveCount(2);
  await expect(page.getByTestId(ids.runLogLine).nth(1)).toHaveAttribute('data-level', 'warning');
  await page.getByTestId(ids.runStop).click();
  // The job stays Running until the batch ends: Stop is spent, the chip is unchanged.
  await expect(page.getByTestId(ids.runStop)).toBeDisabled();
  await expect(page.getByTestId(ids.runStop)).toContainText('Stopping');
  await expect(page.getByTestId(ids.runChip)).toHaveAttribute('data-status', 'running');
  expect(stopped).toBe(1);
});

test('RunStep_WhenJobFinished_ShouldShowTheOutcomeChip', async ({ mount, page }) => {
  const outcomes = [
    ['done', 'Done'],
    ['doneWithErrors', 'Done with errors'],
    ['stopped', 'Stopped'],
  ] as const;
  for (const [status, label] of outcomes) {
    await stubJob(page, importJob({ status }));
    const component = await mount(<RunStep {...jobProps} />);
    await expect(page.getByTestId(ids.runChip)).toHaveText(label);
    await expect(page.getByTestId(ids.runStop)).toHaveCount(0);
    await expect(page.getByTestId(ids.runRerun)).toBeVisible();
    await component.unmount();
    await page.unroute('**/api/import/jobs/job1');
  }
});

test('RunStep_WhenJobFailed_ShouldShowItsMessage', async ({ mount, page }) => {
  const job = importJob({ status: 'failed' });
  await stubJob(page, {
    ...job,
    progress: { ...job.progress, message: 'Jira rejected the token.' },
  });
  await mount(<RunStep {...jobProps} />);
  await expect(page.getByTestId(ids.runChip)).toHaveText('Failed');
  await expect(page.getByTestId(ids.runMessage)).toHaveText('Jira rejected the token.');
});

test('RunStep_WhenStopIsRefused_ShouldShowTheServersMessage', async ({ mount, page }) => {
  await stubJob(page, importJob());
  await page.route('**/api/import/jobs/job1/stop', (route) =>
    json(route, { code: 'conflict', error: 'The job already finished.' }, 409),
  );
  await mount(<RunStep {...jobProps} />);
  await page.getByTestId(ids.runStop).click();
  await expect(page.getByTestId(ids.runStopError)).toHaveText('The job already finished.');
});

const finished = () => importJob({ status: 'doneWithErrors' });

test('RunStep_WhenRetrying_ShouldShowBusyAndLockEveryAction', async ({ mount, page }) => {
  await stubJob(page, finished(), undefined, [
    failedRow,
    { ...failedRow, id: 'e2', sourceKey: 'PLATFORM-9/1' },
  ]);
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => (release = resolve));
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/job1/retry', async (route) => {
    bodies.push(route.request().postDataJSON());
    await gate;
    return json(route, finished());
  });
  await mount(<RunStep {...jobProps} />);
  const first = page.getByTestId(ids.runErrorRow).first();
  await first.getByTestId(ids.runErrorRetry).click();
  await expect(first.getByTestId(ids.runErrorBusy)).toBeVisible();
  await expect(page.getByTestId(ids.runRetryAll)).toBeDisabled();
  await expect(page.getByTestId(ids.runErrorRetry)).toBeDisabled();
  release();
  await expect(page.getByTestId(ids.runErrorBusy)).toHaveCount(0);
  expect(bodies).toEqual([{ errorId: 'e1' }]);
});

test('RunStep_WhenRetryAll_ShouldSendNoErrorId', async ({ mount, page }) => {
  await stubJob(page, finished(), undefined, [failedRow]);
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/job1/retry', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, finished());
  });
  await mount(<RunStep {...jobProps} />);
  await page.getByTestId(ids.runRetryAll).click();
  await expect.poll(() => bodies).toEqual([{ errorId: null }]);
});

test('RunStep_WhenSkipping_ShouldSendTheErrorId', async ({ mount, page }) => {
  await stubJob(page, finished(), undefined, [failedRow]);
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/job1/skip', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ status: 'done' }));
  });
  await mount(<RunStep {...jobProps} />);
  await page.getByTestId(ids.runErrorSkip).click();
  await expect.poll(() => bodies).toEqual([{ errorId: 'e1' }]);
});

test('RunStep_WhenJobStillRuns_ShouldDisableTheErrorActions', async ({ mount, page }) => {
  await stubJob(page, importJob(), undefined, [failedRow]);
  await mount(<RunStep {...jobProps} />);
  // While the job runs the actions are off: the server would answer 409.
  await expect(page.getByTestId(ids.runErrorRetry)).toBeDisabled();
  await expect(page.getByTestId(ids.runRetryAll)).toBeDisabled();
});

test('RunStep_WhenServerRefusesRetry_ShouldShowItsMessage', async ({ mount, page }) => {
  await stubJob(page, finished(), undefined, [failedRow]);
  await page.route('**/api/import/jobs/job1/retry', (route) =>
    json(route, { code: 'conflict', error: 'The job is running.' }, 409),
  );
  await mount(<RunStep {...jobProps} />);
  await page.getByTestId(ids.runErrorRetry).click();
  await expect(page.getByTestId(ids.runErrorsNote)).toHaveText('The job is running.');
});

test('RunStep_WhenJobIsGone_ShouldSayWhy', async ({ mount, page }) => {
  await page.route('**/api/import/jobs/job1', (route) =>
    json(route, { code: 'not_found', error: 'That import was not found.' }, 404),
  );
  await mount(<RunStep {...jobProps} />);
  await expect(page.getByTestId(ids.runJobError)).toHaveText('That import was not found.');
});
