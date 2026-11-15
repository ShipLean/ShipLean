import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { touchTestIds } from '../../../touch/testIds';
import {
  stubShell,
  PEOPLE,
  SHELL_MEMBERS,
  SHELL_SPRINTS,
} from '../../../pages/screenshots/shellStub';
import {
  attachments,
  comments,
  revisions,
  savedCardStory,
  workItemStates,
} from '../../../pages/screenshots/fixtures/workItem';
import { workItemHarnessIds as harness } from '../ct/harnessIds';
import { story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { stubSide } from '../ct/stubSide';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

// The work item page's frame on the CT phone project (390×844, SHIP-714): the section chips and the Save bar.

test('SectionChips_WhenTapped_ShouldScrollToSection', async ({ mount, page }) => {
  await stubShell(page);
  await stubApi(page, savedCardStory);
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ json: workItemStates }));
  await page.route('**/api/projects/p1/members', (route) => route.fulfill({ json: SHELL_MEMBERS }));
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: SHELL_SPRINTS }));
  await page.route('**/api/work-items/*/comments', (route) => route.fulfill({ json: comments }));
  await stubSide(page, PEOPLE.priya, attachments, revisions);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } });
  const chips = page.getByTestId(ids.sectionChip);
  const history = page.getByTestId(ids.history);
  await expect(chips.first()).toHaveAttribute('aria-current', 'true');
  await expect(page.getByTestId(ids.attachment)).toHaveCount(attachments.length);
  await expect(history).not.toBeInViewport();

  const historyChip = chips.filter({ hasText: 'History' });
  await expect(historyChip).toHaveText(`History${revisions.length}`);
  await historyChip.tap();

  await expect(history).toBeInViewport();
  await expect(historyChip).toHaveAttribute('aria-current', 'true');
  // Clear of the pinned top bar.
  const bar = await page.getByTestId(touchTestIds.topBar).boundingBox();
  const top = await history.boundingBox();
  expect(top!.y).toBeGreaterThanOrEqual(bar!.y + bar!.height);
});

test('WorkItemForm_WhenDiscardTapped_ShouldDropDraft', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-141' },
  });
  const bar = page.getByTestId(touchTestIds.saveBar);
  await expect(bar).toHaveCount(0);

  await screen.getByTestId(ids.points).fill('8');
  await screen.getByTestId(ids.title).fill('Pay with a new card');

  await expect(bar.getByTestId(touchTestIds.saveBarMessage)).toHaveText('Title, Story points');
  await bar.getByTestId(touchTestIds.saveBarDiscard).tap();

  await expect(bar).toHaveCount(0);
  await expect(screen.getByTestId(ids.title)).toHaveValue('Pay with saved card');
  await expect(screen.getByTestId(ids.points)).toHaveValue('5');
  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-141');
});
