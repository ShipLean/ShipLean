import { expect, test } from '@playwright/experimental-ct-react';
import type { ConflictDto } from '../../../api/workItems';
import { componentTestIds } from '../../../components/testIds';
import { mei, story } from '../ct/fixtures';
import { workItemHarnessIds as harness } from '../ct/harnessIds';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

const theirs = { ...story, revision: 8, priority: 1, points: 3, changedBy: mei };
const conflict: ConflictDto = {
  error: 'Someone else saved this item first.',
  code: 'conflict',
  current: theirs,
  theirChanges: [
    {
      rev: 8,
      changedBy: mei,
      changedAt: '2026-10-21T09:00:00+00:00',
      changedAtLabel: '21 Oct',
      origin: 'user',
      agentName: null,
      webhook: null,
      changes: [
        { field: 'priority', from: 2, to: 1, fromLabel: '2 - High', toLabel: '1 - Critical' },
        { field: 'points', from: 5, to: 3, fromLabel: '5', toLabel: '3' },
      ],
    },
  ],
};

async function staleSave(page: Parameters<typeof stubApi>[0]) {
  const api = await stubApi(page, story, (patch) =>
    patch.expectedRevision === 7
      ? { status: 409, body: conflict }
      : {
          status: 200,
          body: { id: story.id, key: story.key, revision: 9, changes: [], notices: [] },
        },
  );
  return api;
}

test('ConflictBanner_WhenSaveIsStale_ShouldListTheirChangesByLabelAndFlagOverlap', async ({
  mount,
  page,
}) => {
  await staleSave(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  await screen.getByTestId(ids.points).fill('13');

  await screen.getByTestId(ids.save).click();

  const banner = screen.getByTestId(ids.conflict);
  await expect(banner).toBeVisible();
  await expect(screen.getByTestId(ids.conflictSummary)).toContainText(
    'Mei Lin saved SHIP-141 first',
  );
  await expect(screen.getByTestId(ids.conflictSummary)).toContainText('revision 7');
  await expect(screen.getByTestId(ids.conflictSummary)).toContainText('revision 8');
  const changes = screen.getByTestId(ids.conflictTheirChange);
  await expect(changes).toHaveCount(2);
  await expect(changes.nth(0)).toContainText('Priority');
  await expect(changes.nth(0)).toContainText('2 - High → 1 - Critical');
  await expect(changes.nth(1)).toContainText('Story points');
  await expect(changes.nth(0).getByTestId(ids.conflictAlsoMine)).toHaveCount(0);
  await expect(changes.nth(1).getByTestId(ids.conflictAlsoMine)).toBeVisible();
  await expect(screen.getByTestId(ids.conflictMine)).toHaveText('Story points');
  await expect(screen.getByTestId(ids.points)).toHaveValue('13');
  await expect(screen.getByTestId(ids.save)).toBeDisabled();
});

test('ConflictBanner_WhenReapplyMine_ShouldKeepMyEditOnTheirRevisionAndSendIt', async ({
  mount,
  page,
}) => {
  const api = await staleSave(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  await screen.getByTestId(ids.points).fill('13');
  await screen.getByTestId(ids.save).click();
  await expect(screen.getByTestId(ids.conflict)).toBeVisible();

  await screen.getByTestId(ids.conflictReapplyMine).click();

  await expect(screen.getByTestId(ids.conflict)).toHaveCount(0);
  await expect(screen.getByTestId(ids.points)).toHaveValue('13');
  await expect(
    screen.getByTestId(ids.priority).getByRole('button', { name: '1', exact: true }),
  ).toHaveAttribute('aria-pressed', 'true');
  await expect(screen.getByTestId(ids.meta)).toContainText('revision 8');
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Story points');
  await expect(screen.getByTestId(ids.save)).toBeEnabled();
  await expect(screen.getByTestId(componentTestIds.toast)).toContainText('revision 8');

  // Their revision is in the cache now, so a clean form stays on it instead of going back to the old one.
  await screen.getByTestId(ids.discard).click();
  await expect(screen.getByTestId(ids.meta)).toContainText('revision 8');
  await expect(screen.getByTestId(ids.points)).toHaveValue('3');
  await screen.getByTestId(ids.points).fill('13');

  api.setCurrent(theirs);
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 9');
  expect(api.patches).toHaveLength(2);
  expect(api.patches[1].expectedRevision).toBe(8);
  expect(api.patches[1].changes.points).toEqual({ value: 13 });
  expect(api.patches[1].changes.priority).toBeNull();
});

test('ConflictBanner_WhenReloadTheirs_ShouldDropMyEditsAndShowTheirRevision', async ({
  mount,
  page,
}) => {
  await staleSave(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  await screen.getByTestId(ids.points).fill('13');
  await screen.getByTestId(ids.save).click();
  await expect(screen.getByTestId(ids.conflict)).toBeVisible();

  await screen.getByTestId(ids.conflictReloadTheirs).click();

  await expect(screen.getByTestId(ids.conflict)).toHaveCount(0);
  await expect(screen.getByTestId(ids.points)).toHaveValue('3');
  await expect(
    screen.getByTestId(ids.priority).getByRole('button', { name: '1', exact: true }),
  ).toHaveAttribute('aria-pressed', 'true');
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  await expect(screen.getByTestId(ids.save)).toBeDisabled();
});

test('ConflictBanner_WhenDiscarded_ShouldGoAway', async ({ mount, page }) => {
  await staleSave(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  await screen.getByTestId(ids.points).fill('13');
  await screen.getByTestId(ids.save).click();
  await expect(screen.getByTestId(ids.conflict)).toBeVisible();

  await screen.getByTestId(ids.discard).click();

  await expect(screen.getByTestId(ids.conflict)).toHaveCount(0);
  await expect(screen.getByTestId(harness.location)).toBeAttached();
});

test('ConflictBanner_WhenAgentMadeTheChange_ShouldNameTheAgent', async ({ mount, page }) => {
  await stubApi(page, story, () => ({
    status: 409,
    body: {
      ...conflict,
      theirChanges: [{ ...conflict.theirChanges[0], agentName: 'Claude Code (claude.ai)' }],
    },
  }));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  await screen.getByTestId(ids.points).fill('13');

  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.conflictSummary)).toContainText(
    'Claude Code (claude.ai) saved SHIP-141 first',
  );
});
