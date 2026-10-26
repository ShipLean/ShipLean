import { expect, test } from '@playwright/experimental-ct-react';
import { backlogView, PROJECT, ROWS } from './ct/fixtures';
import { EPIC_ROWS, FEATURE_ROWS, levelView } from './ct/levelFixtures';
import { NewItemLinkHarness } from './ct/NewItemLinkHarness';
import { stubBacklog } from './ct/stubApi';
import { stubLevel } from './ct/stubLevels';
import { backlogTestIds as ids } from './testIds';

const at = (level: string, search = '') => ({
  hooksConfig: { route: `/p/SHIP/backlogs/${level}${search}` },
});

test('Stories_WhenOpenedWithNewBug_ShouldOpenTheRowWithBugPicked', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(
    <NewItemLinkHarness project={PROJECT} level="stories" kind="bug" />,
    at('stories', '?new=bug'),
  );

  await expect(page.getByTestId(ids.addRow)).toBeVisible();
  await expect(page.getByTestId(ids.addType)).toHaveValue('bug');
  await expect(page.getByTestId(ids.addTitle)).toBeFocused();
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/stories');
});

test('Stories_WhenAlreadyOpenAndNewBugArrives_ShouldOpenTheRow', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<NewItemLinkHarness project={PROJECT} level="stories" kind="bug" />, at('stories'));
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);

  await page.getByTestId('harness-link').click();

  await expect(page.getByTestId(ids.addRow)).toBeVisible();
  await expect(page.getByTestId(ids.addType)).toHaveValue('bug');
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/stories');
});

test('Epics_WhenOpenedWithNewEpic_ShouldOpenTheEpicRow', async ({ mount, page }) => {
  await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(
    <NewItemLinkHarness project={PROJECT} level="epics" kind="epic" />,
    at('epics', '?new=epic'),
  );

  await expect(page.getByTestId(ids.addRow)).toBeVisible();
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/epics');
});

test('Features_WhenOpenedWithNewFeature_ShouldOpenTheNewFeatureDialog', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(
    <NewItemLinkHarness project={PROJECT} level="features" kind="feature" />,
    at('features', '?new=feature'),
  );

  await expect(page.getByTestId(ids.newFeatureTitle)).toBeVisible();
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/features');
});

test('Stories_WhenOpenedWithNewEpic_ShouldIgnoreItAndDropTheQuery', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(
    <NewItemLinkHarness project={PROJECT} level="stories" kind="epic" />,
    at('stories', '?new=epic'),
  );

  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/stories');
  // The obvious wrong implementation opens the row for any kind.
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
});

test('Stories_WhenOpenedWithNewTask_ShouldIgnoreItAndDropTheQuery', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(
    <NewItemLinkHarness project={PROJECT} level="stories" kind="task" />,
    at('stories', '?new=task'),
  );

  // "task" is not a kind, so the parsed route query never holds it: only the raw param shows it.
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/stories');
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
});
