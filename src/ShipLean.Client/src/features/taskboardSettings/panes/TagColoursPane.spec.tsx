import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT_ID } from '../../boardSettings/ct/boardSettingsFixtures';
import { stubSettings, taskboardBoard } from '../ct/stubSettings';
import { TaskboardSettingsDialog } from '../TaskboardSettingsDialog';
import { taskboardSettingsTestIds as ids } from '../testIds';

const dialog = (
  <TaskboardSettingsDialog
    projectId={PROJECT_ID}
    section="tags"
    onSectionChange={() => undefined}
    onClose={() => undefined}
  />
);

// SWATCHES order: danger, warn, ok, ...
const OK = 2;

test('TaskboardSettings_WhenTagColourSet_ShouldPutAtOnce', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({}), ['Blocked', 'Customer']);
  await mount(dialog);
  await expect(page.getByTestId(ids.tagRow)).toHaveCount(2);

  await page
    .getByTestId(ids.tagRow)
    .filter({ hasText: 'Blocked' })
    .getByTestId(ids.tagSwatch)
    .nth(OK)
    .click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.tagColours).toEqual([{ tag: 'Blocked', colour: 'ok' }]);
  expect(puts[0].basedOn.tagColours).toEqual([]);
  expect(puts[0].settings.styleRules).toEqual(puts[0].basedOn.styleRules);
});

test('TaskboardSettings_WhenTagColourCleared_ShouldPutWithoutTheEntry', async ({ mount, page }) => {
  const puts = await stubSettings(
    page,
    taskboardBoard({ tagColours: [{ tag: 'Blocked', colour: 'warn' }] }),
    ['Blocked'],
  );
  await mount(dialog);

  await page.getByTestId(ids.tagClear).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.tagColours).toEqual([]);
});

test('TaskboardSettings_WhenNoTags_ShouldSaySo', async ({ mount, page }) => {
  await stubSettings(page, taskboardBoard({}), []);
  await mount(dialog);

  await expect(page.getByTestId(ids.tagsEmpty)).toBeVisible();
});
