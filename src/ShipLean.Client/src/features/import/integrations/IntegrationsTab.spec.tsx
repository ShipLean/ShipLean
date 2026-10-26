import { expect, test } from '@playwright/experimental-ct-react';
import type { ImportJobDto } from '../../../api/import';
import { connection, json, project, stubImport } from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';
import { IntegrationsTab } from './IntegrationsTab';

const job: ImportJobDto = {
  id: 'j1',
  jiraProjectKey: 'PLATFORM',
  projectKey: 'SHIP',
  projectId: project.id,
  status: 'done',
  progress: {
    percent: 100,
    step: null,
    message: '',
    rerun: false,
    counts: {},
    usersMatched: 0,
    usersCreated: 0,
    statesCreated: 0,
    rerunResult: null,
  } as unknown as ImportJobDto['progress'],
  startedAt: null,
  startedLabel: '2 days ago',
  finishedAt: null,
  finishedLabel: '2 days ago',
  createdBy: null,
};

test('IntegrationsTab_WhenJobsExist_ShouldListThemLinkedToTheStepper', async ({ mount, page }) => {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) => json(route, [job]));
  await mount(<IntegrationsTab project={project} />);
  await expect(page.getByTestId(ids.tokenSaved)).toContainText('3fA9');
  await expect(page.getByTestId(ids.jobRow)).toContainText('PLATFORM → SHIP');
  await expect(page.getByTestId(ids.jobLink)).toHaveAttribute('href', '/p/SHIP/import?job=j1');
  await expect(page.getByTestId(ids.openImport)).toHaveAttribute('href', '/p/SHIP/import');
});

test('IntegrationsTab_WhenNoJobs_ShouldSayNoImportsYet', async ({ mount, page }) => {
  await stubImport(page, null);
  await page.route(`**/api/projects/${project.id}/import/jobs`, (route) => json(route, []));
  await mount(<IntegrationsTab project={project} />);
  await expect(page.getByTestId(ids.jobsEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.baseUrl)).toBeVisible();
});

test('IntegrationsTab_WhenMember_ShouldSayAdminsOnlyAndAskNothing', async ({ mount, page }) => {
  const asked: string[] = [];
  await page.route('**/api/**', (route) => {
    asked.push(route.request().url());
    return json(route, [], 403);
  });
  await mount(<IntegrationsTab project={{ ...project, role: 'member' }} />);
  await expect(page.getByTestId(ids.integrationsAdminOnly)).toContainText('Only project admins');
  await expect(page.getByTestId(ids.connectionPanel)).toHaveCount(0);
  await expect(page.getByRole('alert')).toHaveCount(0);
  expect(asked).toEqual([]);
});
