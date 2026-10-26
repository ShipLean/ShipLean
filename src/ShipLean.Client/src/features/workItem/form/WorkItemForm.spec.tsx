import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../components/testIds';
import { workItemHarnessIds as harness } from '../ct/harnessIds';
import { bug, mei, story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';
import { STALE_MESSAGE } from './WorkItemForm';

// A description whose table is wider than the main column at any viewport.
const wideTableHtml = `<table><tbody><tr>${Array.from(
  { length: 12 },
  (_, i) => `<td><code>column_${i}_with_a_long_name</code></td>`,
).join('')}</tr></tbody></table>`;

const route = (key: string) => ({ hooksConfig: { route: `/p/SHIP/items/${key}` } });

test('WorkItemForm_WhenClean_ShouldDisableSaveAndShowSavedRevision', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await expect(screen.getByTestId(ids.title)).toHaveValue('Pay with saved card');
  await expect(screen.getByTestId(ids.save)).toBeDisabled();
  await expect(screen.getByTestId(ids.discard)).toBeHidden();
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 7');
  await expect(screen.getByTestId(harness.tasks)).toBeAttached();
  await expect(screen.getByTestId(harness.links)).toBeAttached();
  await expect(screen.getByTestId(harness.side)).toBeAttached();
  await expect(screen.getByTestId(harness.discussion)).toBeAttached();
});

test('WorkItemForm_WhenFieldEdited_ShouldTrackDirtyAndDiscardRestores', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('8');

  await expect(screen.getByTestId(ids.save)).toBeEnabled();
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Story points');
  await expect(screen.getByTestId(ids.changedDot)).toHaveCount(1);

  await screen.getByTestId(ids.discard).click();

  await expect(screen.getByTestId(ids.points)).toHaveValue('5');
  await expect(screen.getByTestId(ids.save)).toBeDisabled();
});

test('WorkItemForm_WhenEditedBackToSaved_ShouldBeClean', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('8');
  await screen.getByTestId(ids.points).fill('5');

  await expect(screen.getByTestId(ids.save)).toBeDisabled();
});

test('WorkItemForm_WhenSaved_ShouldSendOnlyChangedFieldsWithExpectedRevision', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('8');
  await screen.getByTestId(ids.tagInput).fill('Mobile');
  await screen.getByTestId(ids.tagInput).press('Enter');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches).toHaveLength(1);
  expect(api.patches[0].expectedRevision).toBe(7);
  expect(api.patches[0].changes.points).toEqual({ value: 8 });
  expect(api.patches[0].changes.tags).toEqual({ value: ['Payments', 'Mobile'] });
  expect(api.patches[0].changes.title).toBeNull();
  expect(api.patches[0].changes.description).toBeNull();
});

test('WorkItemForm_WhenPointsCleared_ShouldSendNull', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.points).toEqual({ value: null });
});

test('WorkItemForm_WhenAssigneeUnset_ShouldSendNullAssignee', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.assigneeButton).click();
  await screen.getByTestId(ids.assigneeOption).first().click();
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.assignee).toEqual({ value: null });
});

test('WorkItemForm_WhenStatePicked_ShouldSendTheStateId', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.stateButton).click();
  await screen.getByTestId(componentTestIds.stateMenuOption).first().click();
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.state).toEqual({ value: 's-new' });
});

test('WorkItemForm_WhenDescriptionEdited_ShouldPreviewTheServerRender', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  const description = screen.getByTestId(ids.description);

  await expect(description.getByTestId(componentTestIds.markdown)).toContainText('returning');
  await description.getByTestId(ids.descriptionEdit).click();
  await description.getByTestId(ids.descriptionInput).fill('Pay by **card**');
  await description.getByTestId(ids.descriptionView).click();

  await expect(description.getByTestId(componentTestIds.markdown)).toHaveText(
    'rendered:Pay by **card**',
  );
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Description');
});

test('WorkItemForm_WhenDescriptionHasAWideTable_ShouldKeepItBesideTheSide', async ({
  mount,
  page,
}) => {
  await stubApi(page, { ...story, descriptionHtml: wideTableHtml });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  const description = screen.getByTestId(ids.descriptionPreview);
  await expect(description.getByTestId(componentTestIds.markdown)).toBeVisible();
  const shown = (await description.boundingBox())!;
  const side = (await screen.getByTestId(harness.side).boundingBox())!;
  expect(shown.x + shown.width).toBeLessThanOrEqual(side.x);
});

test('WorkItemForm_WhenStory_ShouldShowCriteriaAndCountMet', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await expect(screen.getByTestId(ids.criteriaMet)).toHaveText('1/2 met');
  await expect(screen.getByTestId(ids.repro)).toHaveCount(0);

  await screen.getByTestId(ids.criterion).nth(1).check();

  await expect(screen.getByTestId(ids.criteriaMet)).toHaveText('2/2 met');
  await screen.getByTestId(ids.save).click();
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.acceptanceCriteria).toEqual({
    value: [
      { text: 'Card is tokenised', done: true },
      { text: 'Declines are explained', done: true },
    ],
  });
});

test('WorkItemForm_WhenBug_ShouldShowReproAndSeverity', async ({ mount, page }) => {
  const api = await stubApi(page, bug);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-153" />, route('SHIP-153'));

  await expect(screen.getByTestId(ids.repro)).toContainText('Open the prompt');
  await expect(screen.getByTestId(ids.severity)).toHaveValue('2');
  await expect(screen.getByTestId(ids.criteria)).toHaveCount(0);

  await screen.getByTestId(ids.severity).selectOption('1');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.severity).toEqual({ value: 1 });
});

test('WorkItemForm_WhenTitleEmptied_ShouldRefuseToSave', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.title).fill('  ');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveError)).toHaveText('A work item needs a title.');
  expect(api.patches).toHaveLength(0);
});

test('WorkItemForm_WhenSaveConflictsWithoutTheirChanges_ShouldKeepEditsAndSayWhy', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, story, () => ({
    status: 409,
    body: { error: 'Stale revision.', code: 'conflict' },
  }));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('13');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveError)).toHaveText(STALE_MESSAGE);
  await expect(screen.getByTestId(ids.points)).toHaveValue('13');
  await expect(screen.getByTestId(ids.save)).toBeEnabled();
  expect(api.patches).toHaveLength(1);
});

test('WorkItemForm_WhenLeavingClean_ShouldNavigate', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await expect(screen.getByTestId(ids.title)).toBeVisible();

  await screen.getByTestId(harness.leave).click();

  await expect(screen.getByTestId(harness.location)).toHaveText('/elsewhere');
});

test('WorkItemForm_WhenLeavingDirty_ShouldStayAndToast', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('8');
  await screen.getByTestId(harness.leave).click();

  await expect(screen.getByTestId(componentTestIds.toast)).toContainText('unsaved changes');
  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-141');
  await expect(screen.getByTestId(ids.points)).toHaveValue('8');
});

test('WorkItemForm_WhenDirty_ShouldRegisterBeforeUnload', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  const prevented = () =>
    page.evaluate(() => {
      const event = new Event('beforeunload', { cancelable: true });
      window.dispatchEvent(event);
      return event.defaultPrevented;
    });

  expect(await prevented()).toBe(false);
  await screen.getByTestId(ids.points).fill('8');
  expect(await prevented()).toBe(true);
});

test('WorkItemForm_WhenLoadFails_ShouldSayWhy', async ({ mount, page }) => {
  await page.route('**/api/projects/p1/work-items/by-key/*', (route) =>
    route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Work item not found.', code: 'not_found' }),
    }),
  );
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-1" />, route('SHIP-1'));

  await expect(screen.getByTestId(ids.loadFailed)).toHaveText('Work item not found.');
});

const newer = { ...story, revision: 8, priority: 1, changedBy: mei };

test('WorkItemForm_WhenNewerRevisionArrivesWhileClean_ShouldReplaceTheForm', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 7');

  api.setCurrent({ ...newer, points: 21 });
  await screen.getByTestId(harness.refetch).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  await expect(screen.getByTestId(ids.points)).toHaveValue('21');
});

test('WorkItemForm_WhenNewerRevisionArrivesWhileDirty_ShouldKeepEditsAndSendTheOldRevision', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, story, () => ({
    status: 409,
    body: { error: 'Stale revision.', code: 'conflict' },
  }));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await screen.getByTestId(ids.points).fill('8');

  api.setCurrent({ ...newer, points: 21 });
  await screen.getByTestId(harness.refetch).click();
  await expect(screen.getByTestId(ids.meta)).toContainText('revision 7');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveError)).toBeVisible();
  await expect(screen.getByTestId(ids.points)).toHaveValue('8');
  expect(api.patches).toHaveLength(1);
  expect(api.patches[0].expectedRevision).toBe(7);
});

test('WorkItemForm_WhenCtrlSPressed_ShouldSave', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await screen.getByTestId(ids.points).fill('8');

  await page.keyboard.press('Control+s');

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches).toHaveLength(1);
  expect(api.patches[0].changes.points).toEqual({ value: 8 });
});

test('WorkItemForm_WhenCtrlSPressedClean_ShouldNotSave', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await expect(screen.getByTestId(ids.title)).toBeVisible();

  await page.keyboard.press('Control+s');

  await expect(screen.getByTestId(ids.save)).toBeDisabled();
  expect(api.patches).toHaveLength(0);
});

test('WorkItemForm_WhenEditedWhileSaving_ShouldKeepTheLaterEdit', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const release = api.holdPatches();
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await screen.getByTestId(ids.points).fill('8');
  await screen.getByTestId(ids.save).click();

  await screen.getByTestId(ids.priority).getByRole('button', { name: '1', exact: true }).click();
  release();

  await expect(screen.getByTestId(ids.meta)).toContainText('revision 8');
  await expect(screen.getByTestId(ids.points)).toHaveValue('8');
  await expect(
    screen.getByTestId(ids.priority).getByRole('button', { name: '1', exact: true }),
  ).toHaveAttribute('aria-pressed', 'true');
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Priority');
});

test('WorkItemForm_WhenSaveWasAPickUp_ShouldToastAssignedToYou', async ({ mount, page }) => {
  await stubApi(page, story, () => ({
    status: 200,
    body: { id: story.id, key: story.key, revision: 8, changes: [], notices: ['pulled'] },
  }));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await screen.getByTestId(ids.points).fill('8');

  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(componentTestIds.toast)).toContainText('Assigned to you');
});

test('WorkItemForm_WhenSavedWithoutNotices_ShouldNotToast', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await screen.getByTestId(ids.points).fill('8');

  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  await expect(screen.getByTestId(componentTestIds.toast)).toHaveCount(0);
});

// After a 200 Save the form reads the item again. A read that answers an older revision, or fails, must not pass for the
// save: the error shows and the draft stays.
test('WorkItemForm_WhenTheReloadAfterSaveReturnsAnOlderRevision_ShouldShowTheErrorAndKeepTheDraft', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await page.route('**/api/projects/p1/work-items/by-key/*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(story) }),
  );
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('8');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveError)).toContainText(
    'Saved as revision 8, but the reload returned revision 7',
  );
  await expect(screen.getByTestId(ids.points)).toHaveValue('8');
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Story points');
});

test('WorkItemForm_WhenTheReloadAfterSaveFails_ShouldShowTheErrorAndKeepTheDraft', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  let saved = false;
  page.on('request', (request) => {
    if (request.method() === 'PATCH') saved = true;
  });
  await page.route('**/api/projects/p1/work-items/by-key/*', (route) => {
    return !saved
      ? route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(story) })
      : route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Something went wrong.', code: 'internal' }),
        });
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await screen.getByTestId(ids.points).fill('8');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveError)).toContainText(
    'Saved as revision 8, but reloading it failed',
  );
  await expect(screen.getByTestId(ids.points)).toHaveValue('8');
});
