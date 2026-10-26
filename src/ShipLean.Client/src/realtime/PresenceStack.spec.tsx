import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../components/testIds';
import { realtimeTestIds as ids } from '../features/realtime/testIds';
import { member } from './testing/presenceFixtures';
import { PresenceStack } from './PresenceStack';

const members = [1, 2, 3, 4, 5].map((n) => member(n));

test('PresenceStack_WhenPhotoUrl_ShouldRenderPhoto', async ({ mount, page }) => {
  await mount(
    <PresenceStack userIds={['user-1']} members={[member(1, '/photos/one.png')]} meId="user-9" />,
  );
  await expect(page.getByTestId(componentTestIds.avatarPhoto)).toHaveAttribute(
    'src',
    '/photos/one.png',
  );
});

test('PresenceStack_WhenMoreThanThree_ShouldShowPlusN', async ({ mount, page }) => {
  await mount(
    <PresenceStack
      userIds={['user-1', 'user-2', 'user-3', 'user-4', 'user-5']}
      members={members}
      meId="user-9"
    />,
  );
  await expect(page.getByTestId(ids.presenceAvatar)).toHaveCount(3);
  await expect(page.getByTestId(ids.presenceMore)).toHaveText('+2');
  await page.getByTestId(ids.presenceToggle).click();
  await expect(page.getByTestId(ids.presenceListItem)).toHaveCount(5);
  await expect(page.getByTestId(ids.presenceList)).toContainText('Person 5');
});

test('PresenceStack_ShouldLeaveMeOut', async ({ mount, page }) => {
  await mount(<PresenceStack userIds={['user-1', 'user-2']} members={members} meId="user-1" />);
  await expect(page.getByTestId(ids.presenceAvatar)).toHaveCount(1);
  await page.getByTestId(ids.presenceToggle).click();
  await expect(page.getByTestId(ids.presenceList)).toContainText('Person 2');
  await expect(page.getByTestId(ids.presenceList)).not.toContainText('Person 1');
});

test('PresenceStack_WhenNobodyElse_ShouldRenderNothing', async ({ mount, page }) => {
  await mount(<PresenceStack userIds={['user-1']} members={members} meId="user-1" />);
  await expect(page.getByTestId(ids.presenceStack)).toHaveCount(0);
});
