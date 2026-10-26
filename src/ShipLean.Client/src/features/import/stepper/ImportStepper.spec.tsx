import { expect, test } from '@playwright/experimental-ct-react';
import {
  connection,
  json,
  jiraProjects,
  mappingDto,
  project,
  stubImport,
  stubMapping,
} from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';
import { ImportStepper } from './ImportStepper';

async function stubWorkingConnection(page: Parameters<typeof stubImport>[0]) {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, { ok: true, who: { displayName: 'Priya Shah', email: null }, message: null }),
  );
}

const pick = async (page: Parameters<typeof stubImport>[0], key: string) => {
  await page.getByTestId(ids.projectRow).filter({ hasText: key }).click();
};

test('ImportStepper_WhenConnectionNotTested_ShouldBlockNext', async ({ mount, page }) => {
  await stubWorkingConnection(page);
  await mount(<ImportStepper project={project} />);
  await expect(page.getByTestId(ids.next)).toBeDisabled();
  await expect(page.getByTestId(ids.step2)).toBeDisabled();
  await page.getByTestId(ids.test).click();
  await expect(page.getByTestId(ids.next)).toBeEnabled();
});

test('ImportStepper_WhenConnectionFails_ShouldKeepNextBlocked', async ({ mount, page }) => {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, { ok: false, who: null, message: 'Jira is not reachable.' }),
  );
  await mount(<ImportStepper project={project} />);
  await page.getByTestId(ids.test).click();
  await expect(page.getByTestId(ids.testResult)).toContainText('Jira is not reachable.');
  await expect(page.getByTestId(ids.next)).toBeDisabled();
});

test('ImportStepper_WhenProjectsListed_ShouldShowImportedAsAndFilter', async ({ mount, page }) => {
  await stubWorkingConnection(page);
  await mount(<ImportStepper project={project} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.projectRow)).toHaveCount(jiraProjects.length);
  await expect(page.getByTestId(ids.projectImportedAs)).toHaveText('Imported as MOB on 2 days ago');
  await expect(page.getByTestId(ids.next)).toBeDisabled();
  await page.getByTestId(ids.projectSearch).fill('plat');
  await expect(page.getByTestId(ids.projectRow)).toHaveCount(1);
  await page.getByTestId(ids.projectSearch).fill('zzz');
  await expect(page.getByTestId(ids.projectsEmpty)).toBeVisible();
});

test('ImportStepper_WhenNoConnectionSaved_ShouldShowWhyProjectsFail', async ({ mount, page }) => {
  await stubImport(page, null);
  await mount(<ImportStepper project={project} />);
  await expect(page.getByTestId(ids.next)).toBeDisabled();
  await expect(page.getByTestId(ids.save)).toBeVisible();
});

test('ImportStepper_WhenProjectEmpty_ShouldImportIntoIt', async ({ mount, page }) => {
  await stubWorkingConnection(page);
  await mount(<ImportStepper project={project} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await pick(page, 'PLATFORM');
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.targetFixed)).toContainText('Imports into SHIP');
  await expect(page.getByTestId(ids.keyExample)).toHaveText('PLATFORM-123 → SHIP-123');
  await stubMapping(page);
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.mappingStep)).toBeVisible();
  await expect(page.getByTestId(ids.runStep)).toHaveCount(0);
});

test('ImportStepper_WhenProjectNotEmpty_ShouldOfferNewProjectAndBlockNext', async ({
  mount,
  page,
}) => {
  await stubWorkingConnection(page);
  await mount(<ImportStepper project={{ ...project, nextItemNumber: 42 }} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await pick(page, 'PLATFORM');
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.targetNonEmpty)).toContainText('SHIP already has work items');
  await expect(page.getByTestId(ids.targetOther)).toHaveAttribute('href', '/import');
  await expect(page.getByTestId(ids.next)).toBeDisabled();
});

test('ImportStepper_WhenImportedIntoThisProject_ShouldRerunEvenWhenNotEmpty', async ({
  mount,
  page,
}) => {
  await stubWorkingConnection(page);
  await stubMapping(page, { ...mappingDto });
  await mount(<ImportStepper project={{ ...project, key: 'MOB', nextItemNumber: 1285 }} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await pick(page, 'MOBILE');
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.targetRerun)).toBeVisible();
  // Next waits for the mapping read: whether step 4 is skipped depends on it.
  await expect(page.getByTestId(ids.next)).toBeEnabled();
});

test('ImportStepper_WhenImportedIntoAnotherProject_ShouldLinkThere', async ({ mount, page }) => {
  await stubWorkingConnection(page);
  await mount(<ImportStepper project={project} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await pick(page, 'MOBILE');
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.targetOther)).toHaveAttribute('href', '/p/MOB/import');
  await expect(page.getByTestId(ids.next)).toBeDisabled();
});

test('ImportStepper_WhenNotAdmin_ShouldSayOnlyAdminsImport', async ({ mount, page }) => {
  await stubImport(page, null);
  await mount(<ImportStepper project={{ ...project, role: 'member' }} />);
  await expect(page.getByTestId(ids.notAdmin)).toBeVisible();
});
