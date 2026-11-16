import { expect, test } from '@playwright/experimental-ct-react';
import { ReviewBar } from '../ReviewBar';
import { taskChangesTestIds as ids } from '../testIds';
import type { ReviewActions } from '../types';
import { ConflictingReviewBar } from './ConflictingReviewBar';
import { CHANGES } from './fixtures';

const outcome = { state: 'Closed', openComments: 0 };
const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-171/changes' } };
const withOpen = (n: number) => ({ ...CHANGES, openComments: n });

function bar(data: typeof CHANGES, review: Partial<ReviewActions> = {}) {
  const actions: ReviewActions = {
    approve: async () => outcome,
    requestChanges: async () => outcome,
    ...review,
  };
  return <ReviewBar data={data} projectKey="SHIP" itemKey="SHIP-171" review={actions} />;
}

test('ReviewBar_WhenApproved_ShouldPostNoteAndWarnAboutOpenComments', async ({ mount, page }) => {
  const calls: (string | null)[] = [];
  await mount(
    bar(withOpen(2), {
      approve: async (note) => {
        calls.push(note);
        return outcome;
      },
    }),
    route,
  );
  await page.getByTestId(ids.approve).click();
  await expect(page.getByTestId(ids.approveWarning)).toContainText('2 comments are still open');
  await page.getByTestId(ids.approveNote).fill('Looks good');
  await page.getByTestId(ids.approveConfirm).click();
  await expect.poll(() => calls).toEqual(['Looks good']);
});

test('ReviewBar_WhenApproveConflicts_ShouldShowReloadMessage', async ({ mount, page }) => {
  await mount(<ConflictingReviewBar />, route);
  await page.getByTestId(ids.approve).click();
  await page.getByTestId(ids.approveConfirm).click();
  await expect(page.getByTestId(ids.reviewError)).toContainText('Reload');
});

test('ReviewBar_WhenRequestingChanges_ShouldPostNoteAndKeepAssignee', async ({ mount, page }) => {
  const calls: [string | null, boolean][] = [];
  await mount(
    bar(withOpen(2), {
      requestChanges: async (note, keep) => {
        calls.push([note, keep]);
        return outcome;
      },
    }),
    route,
  );
  await page.getByTestId(ids.requestChanges).click();
  await page.getByTestId(ids.requestNote).fill('Fix both');
  await page.getByTestId(ids.requestKeepAssignee).check();
  await page.getByTestId(ids.requestConfirm).click();
  await expect.poll(() => calls).toEqual([['Fix both', true]]);
});

test('ReviewBar_WhenCommentsAreOpen_ShouldShowTheCountAndDisableWithNone', async ({
  mount,
  page,
}) => {
  const component = await mount(bar(withOpen(3)), route);
  await expect(page.getByTestId(ids.requestChanges)).toHaveText('Request changes · 3');
  await expect(page.getByTestId(ids.requestChanges)).toBeEnabled();
  await component.update(bar(withOpen(0)));
  await expect(page.getByTestId(ids.requestChanges)).toBeDisabled();
});
