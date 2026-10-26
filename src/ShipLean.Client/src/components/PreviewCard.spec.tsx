import { expect, test } from '@playwright/experimental-ct-react';
import { PreviewCard } from './PreviewCard';
import { componentTestIds as ids } from './testIds';

test('PreviewCard_ShouldRenderTitleAndBody', async ({ mount, page }) => {
  await mount(
    <PreviewCard
      type="story"
      itemKey="SHIP-12"
      title="Reset password"
      stateName="Active"
      stateCategory="inProgress"
      assignee={{ initials: 'JD', avatarColour: '#6f45bf' }}
    >
      <span>2 of 4 tasks done</span>
    </PreviewCard>,
  );
  await expect(page.getByTestId(ids.previewCardKey)).toHaveText('SHIP-12');
  await expect(page.getByTestId(ids.previewCardTitle)).toHaveText('Reset password');
  await expect(page.getByTestId(ids.previewCardState)).toContainText('Active');
  await expect(page.getByTestId(ids.previewCardBody)).toContainText('2 of 4 tasks done');
  await expect(page.getByTestId(ids.avatar)).toHaveText('JD');
});

test('PreviewCard_WhenUnassigned_ShouldShowADashedAvatar', async ({ mount, page }) => {
  await mount(
    <PreviewCard
      type="bug"
      itemKey="SHIP-3"
      title="Crash"
      stateName="New"
      stateCategory="proposed"
      assignee={null}
    />,
  );
  await expect(page.getByTestId(ids.avatar)).toHaveAttribute('data-variant', 'none');
  await expect(page.getByTestId(ids.previewCardBody)).toHaveCount(0);
});
