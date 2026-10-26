import { expect, test } from '@playwright/experimental-ct-react';
import type { MountResult } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { ImportMappingDto } from '../../../api/import';
import { ImportStepper } from '../stepper/ImportStepper';
import {
  connection,
  importJob,
  json,
  mappingDto,
  mappingWithNewStatus,
  project,
  sam,
  stubImport,
  stubJob,
  stubMapping,
} from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';

type Mount = (component: React.JSX.Element) => Promise<MountResult>;

// Steps 1 to 3 for `jiraKey`, then Next into whatever follows step 3.
async function openMapping(
  mount: Mount,
  page: Page,
  options: { jiraKey?: string; mapping?: ImportMappingDto; rerun?: boolean } = {},
) {
  const { jiraKey = 'PLATFORM', mapping = mappingDto, rerun = false } = options;
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, { ok: true, who: { displayName: 'Priya Shah', email: null }, message: null }),
  );
  const requests = await stubMapping(page, mapping);
  await mount(
    <ImportStepper project={rerun ? { ...project, key: 'MOB', nextItemNumber: 9 } : project} />,
  );
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.projectRow).filter({ hasText: jiraKey }).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.next).click();
  return requests;
}

const typeRow = (page: Page, jiraType: string) =>
  page.locator(`[data-testid="${ids.mappingTypeRow}"][data-jira-type="${jiraType}"]`);
const statusRow = (page: Page, jiraType: string, status: string) =>
  page
    .locator(`[data-testid="${ids.mappingStatusGroup}"][data-jira-type="${jiraType}"]`)
    .getByTestId(ids.mappingStatusRow)
    .filter({ hasText: status });

test('MappingStep_WhenOpened_ShouldAskForTheMappingOfThePickedProject', async ({ mount, page }) => {
  const requests = await openMapping(mount, page);
  await expect(page.getByTestId(ids.mappingStep)).toBeVisible();
  expect(requests).toEqual([
    { connectionId: 'c1', jiraProjectKey: 'PLATFORM', previousJobId: null },
  ]);
  await expect(page.getByTestId(ids.mappingTotals)).toContainText('1,200 issues');
  await expect(page.getByTestId(ids.mappingWarning)).toHaveCount(2);
  await expect(page.getByTestId(ids.mappingNewChip)).toHaveCount(0);
});

test('MappingStep_WhenWarningReviewClicked_ShouldFocusItsSection', async ({ mount, page }) => {
  await openMapping(mount, page);
  await page.getByTestId(ids.mappingWarning).nth(1).getByTestId(ids.mappingWarningLink).click();
  await expect(page.getByTestId(ids.mappingUsers)).toBeFocused();
  await page.getByTestId(ids.mappingWarning).nth(0).getByTestId(ids.mappingWarningLink).click();
  await expect(page.getByTestId(ids.mappingTypes)).toBeFocused();
});

test('MappingStep_WhenTypeChanged_ShouldRegroupItsStatuses', async ({ mount, page }) => {
  await openMapping(mount, page);
  await expect(page.getByTestId(ids.mappingStatusGroup)).toHaveCount(2);
  await typeRow(page, 'Spike').getByTestId(ids.mappingTypeSelect).selectOption('task');
  await expect(page.getByTestId(ids.mappingStatusGroup)).toHaveCount(3);
  // The task type has no state called Open, so the run creates one.
  await expect(
    statusRow(page, 'Spike', 'Open').getByTestId(ids.mappingStatusCreates),
  ).toBeVisible();
  await typeRow(page, 'Bug').getByTestId(ids.mappingTypeSelect).selectOption('skip');
  await expect(page.getByTestId(ids.mappingStatusGroup)).toHaveCount(2);
});

test('MappingStep_WhenStatusMapped_ShouldPickAnExistingStateOrCreateOne', async ({
  mount,
  page,
}) => {
  await openMapping(mount, page);
  const backlog = statusRow(page, 'Story', 'Backlog');
  await expect(backlog.getByTestId(ids.mappingStatusCreates)).toHaveCount(0);
  await backlog.getByTestId(ids.mappingStatusSelect).selectOption('Active');
  await expect(backlog.getByTestId(ids.mappingStatusSelect)).toHaveValue('Active');
  await expect(backlog.getByTestId('state-dot')).toHaveAttribute('data-category', 'inProgress');
  await backlog.getByTestId(ids.mappingStatusSelect).selectOption('__new');
  await expect(backlog.getByTestId(ids.mappingStatusCreates)).toContainText('board column');
  await backlog.getByTestId(ids.mappingStatusCategory).selectOption('resolved');
  await expect(backlog.getByTestId(ids.mappingStatusCategory)).toHaveValue('resolved');
});

test('MappingStep_WhenUsersListed_ShouldHideMatchedAndOfferTheChoices', async ({ mount, page }) => {
  await openMapping(mount, page);
  const rows = page.getByTestId(ids.mappingUserRow);
  await expect(rows).toHaveCount(2);
  await page.getByTestId(ids.mappingShowMatched).click();
  await expect(rows).toHaveCount(3);
  const dev = rows.filter({ hasText: 'Dev Patel' }).getByTestId(ids.mappingUserSelect);
  await expect(dev).toHaveValue('create');
  await dev.selectOption(sam.id);
  await expect(dev).toHaveValue(sam.id);
  await dev.selectOption('jiraImportUser');
  await expect(dev).toHaveValue('jiraImportUser');
  // An automation account is never offered Create.
  const bot = rows.filter({ hasText: 'Jira Automation' }).getByTestId(ids.mappingUserSelect);
  await expect(bot.locator('option[value="create"]')).toHaveCount(0);
});

test('MappingStep_WhenFieldsEdited_ShouldChangeTheFieldAndOptions', async ({ mount, page }) => {
  await openMapping(mount, page);
  await expect(page.getByTestId(ids.mappingStoryPointsNote)).toContainText('100');
  await page.getByTestId(ids.mappingStoryPoints).selectOption('customfield_10020');
  await expect(page.getByTestId(ids.mappingStoryPointsNote)).toContainText('880');
  await expect(page.getByTestId(ids.mappingIncludeComments)).toBeChecked();
  await page.getByTestId(ids.mappingIncludeComments).uncheck();
  await expect(page.getByTestId(ids.mappingIncludeComments)).not.toBeChecked();
  await page.getByTestId(ids.mappingIncludeAttachments).check();
  await expect(page.getByTestId(ids.mappingIncludeAttachments)).toBeChecked();
});

test('MappingStep_WhenLeftAndReopened_ShouldKeepTheEdits', async ({ mount, page }) => {
  await openMapping(mount, page);
  await page.getByTestId(ids.mappingIncludeComponents).uncheck();
  await typeRow(page, 'Spike').getByTestId(ids.mappingTypeSelect).selectOption('task');
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.runStep)).toBeVisible();
  await page.getByTestId(ids.back).click();
  await expect(page.getByTestId(ids.mappingIncludeComponents)).not.toBeChecked();
  await expect(typeRow(page, 'Spike').getByTestId(ids.mappingTypeSelect)).toHaveValue('task');
});

test('MappingStep_WhenNothingIsImported_ShouldNotReachRun', async ({ mount, page }) => {
  await openMapping(mount, page);
  await expect(page.getByTestId(ids.next)).toBeEnabled();
  await expect(page.getByTestId(ids.step5)).toBeEnabled();
  await typeRow(page, 'Story').getByTestId(ids.mappingTypeSelect).selectOption('skip');
  await typeRow(page, 'Bug').getByTestId(ids.mappingTypeSelect).selectOption('skip');
  await expect(page.getByTestId(ids.next)).toBeDisabled();
  await expect(page.getByTestId(ids.step5)).toBeDisabled();
  await expect(page.getByTestId(ids.hint)).toContainText('at least one type');
  await typeRow(page, 'Bug').getByTestId(ids.mappingTypeSelect).selectOption('bug');
  await expect(page.getByTestId(ids.next)).toBeEnabled();
});

test('MappingStep_WhenAPersonMatchedToNobody_ShouldNotReachRun', async ({ mount, page }) => {
  const orphan: ImportMappingDto = {
    ...mappingDto,
    users: mappingDto.users.map((u) => (u.resolution === 'matched' ? { ...u, user: null } : u)),
  };
  await openMapping(mount, page, { mapping: orphan });
  await expect(page.getByTestId(ids.next)).toBeDisabled();
  await expect(page.getByTestId(ids.step5)).toBeDisabled();
});

test('MappingStep_WhenRerunHasNewRows_ShouldShowTheWholeMappingWithThemMarked', async ({
  mount,
  page,
}) => {
  const requests = await openMapping(mount, page, {
    jiraKey: 'MOBILE',
    rerun: true,
    mapping: mappingWithNewStatus,
  });
  expect(requests).toEqual([{ connectionId: 'c1', jiraProjectKey: 'MOBILE', previousJobId: 'j1' }]);
  await expect(page.getByTestId(ids.mappingStep)).toBeVisible();
  await expect(page.getByTestId(ids.mappingStatusRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.mappingNewChip)).toHaveCount(1);
  await expect(statusRow(page, 'Story', 'In Review')).toHaveAttribute('data-new', 'true');
  await expect(statusRow(page, 'Story', 'Backlog')).toHaveAttribute('data-new', 'false');
});

test('MappingStep_WhenRerunHasNothingNew_ShouldSkipToRun', async ({ mount, page }) => {
  await openMapping(mount, page, {
    jiraKey: 'MOBILE',
    rerun: true,
    mapping: mappingDto,
  }).then(() => undefined);
  // Next from step 3 went straight to step 5.
  await expect(page.getByTestId(ids.runReady)).toContainText('Ready to re-run');
  await expect(page.getByTestId(ids.mappingStep)).toHaveCount(0);
  await expect(page.getByTestId(ids.step4)).toContainText('Same as last run');
  await page.getByTestId(ids.back).click();
  await expect(page.getByTestId(ids.targetStep)).toBeVisible();
  await expect(page.getByTestId(ids.next)).toContainText('Next: Run');
});

test('MappingStep_WhenMappingCannotBeRead_ShouldSayWhy', async ({ mount, page }) => {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, { ok: true, who: null, message: null }),
  );
  await page.route('**/api/import/mapping', (route) =>
    json(route, { error: 'Jira is not reachable.', code: 'validation' }, 400),
  );
  await mount(<ImportStepper project={project} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.projectRow).filter({ hasText: 'PLATFORM' }).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.mappingError)).toContainText('Jira is not reachable.');
  await expect(page.getByTestId(ids.step5)).toBeDisabled();
});

test('MappingStep_WhenEditedOnASkippedMapping_ShouldRerunWithTheEdit', async ({ mount, page }) => {
  await openMapping(mount, page, { jiraKey: 'MOBILE', rerun: true, mapping: mappingDto });
  const bodies: { mapping: ImportMappingDto | null }[] = [];
  await page.route('**/api/import/jobs/j1/rerun', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ id: 'j1', status: 'queued' }));
  });
  await stubJob(page, importJob({ id: 'j1', status: 'queued' }));
  await page.getByTestId(ids.step4).click();
  await page.getByTestId(ids.mappingIncludeComponents).uncheck();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.runRerun).click();
  await expect.poll(() => bodies).toHaveLength(1);
  expect(bodies[0].mapping?.fields.includeComponents).toBe(false);
});

test('MappingStep_WhenRerunMapsNothingEdited_ShouldRerunWithNoMapping', async ({ mount, page }) => {
  await openMapping(mount, page, { jiraKey: 'MOBILE', rerun: true, mapping: mappingDto });
  const bodies: unknown[] = [];
  await page.route('**/api/import/jobs/j1/rerun', (route) => {
    bodies.push(route.request().postDataJSON());
    return json(route, importJob({ id: 'j1', status: 'queued' }));
  });
  await stubJob(page, importJob({ id: 'j1', status: 'queued' }));
  await page.getByTestId(ids.runRerun).click();
  await expect.poll(() => bodies).toEqual([{ mapping: null }]);
});

test('MappingStep_WhenTheOnlyNewPersonMatchedByEmail_ShouldListThemWithoutShowMatched', async ({
  mount,
  page,
}) => {
  const newPerson = {
    ...mappingDto,
    users: mappingDto.users.map((u) =>
      u.resolution === 'matched' ? { ...u, isNewSinceLastRun: true } : u,
    ),
  };
  await openMapping(mount, page, { jiraKey: 'MOBILE', rerun: true, mapping: newPerson });
  const row = page.locator(`[data-testid="${ids.mappingUserRow}"][data-resolution="matched"]`);
  await expect(row).toHaveAttribute('data-new', 'true');
});

test('MappingStep_WhenARerunMappingCannotBeRead_ShouldSayWhyOnTheTargetStep', async ({
  mount,
  page,
}) => {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, { ok: true, who: null, message: null }),
  );
  await page.route('**/api/import/mapping', (route) =>
    json(route, { error: 'Jira is not reachable.', code: 'validation' }, 400),
  );
  await mount(<ImportStepper project={{ ...project, key: 'MOB', nextItemNumber: 9 }} />);
  await page.getByTestId(ids.test).click();
  await page.getByTestId(ids.next).click();
  await page.getByTestId(ids.projectRow).filter({ hasText: 'MOBILE' }).click();
  await page.getByTestId(ids.next).click();
  await expect(page.getByTestId(ids.targetStep)).toBeVisible();
  await expect(page.getByTestId(ids.mappingError)).toContainText('Jira is not reachable.');
});
