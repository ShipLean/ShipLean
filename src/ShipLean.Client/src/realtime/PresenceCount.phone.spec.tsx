import { expect, test } from '@playwright/experimental-ct-react';
import { realtimeTestIds as ids } from '../features/realtime/testIds';
import { touchTestIds } from '../touch/testIds';
import { LiveDot } from './LiveDot';
import { PresenceStack } from './PresenceStack';
import { member } from './testing/presenceFixtures';

// Presence and the Live dot in the touch top bar (SHIP-713): a count only, yourself left out, that lists everyone in a
// sheet; Live is a dot and Reconnecting keeps its word.

const members = [1, 2, 3, 4, 5].map((n) => member(n));

test('PresenceCount_WhenFourOthersHere_ShouldShowPlusFourAndListThemInSheet', async ({
  mount,
  page,
}) => {
  await mount(
    <PresenceStack
      userIds={['user-1', 'user-2', 'user-3', 'user-4', 'user-5']}
      members={members}
      meId="user-1"
    />,
  );
  const count = page.getByTestId(ids.presenceCount);
  await expect(count).toHaveText('+4');
  await expect(page.getByTestId(ids.presenceAvatar)).toHaveCount(0);
  const box = (await count.boundingBox())!;
  expect(Math.min(box.width, box.height)).toBeGreaterThanOrEqual(32);

  await count.tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(ids.presenceSheetItem)).toHaveText([
    'P2Person 2',
    'P3Person 3',
    'P4Person 4',
    'P5Person 5',
  ]);
  await sheet.getByTestId(touchTestIds.sheetClose).tap();
  await expect(sheet).toHaveCount(0);
});

test('LiveDot_WhenReconnecting_ShouldSayReconnecting', async ({ mount, page }) => {
  const dot = page.getByTestId(ids.liveDot);
  const component = await mount(<LiveDot state="live" />);
  await expect(dot).toHaveAttribute('data-state', 'live');
  // Live is the dot alone: its word is for screen readers only and takes no width.
  await expect(dot).toHaveText('Live');
  expect((await dot.boundingBox())!.width).toBeLessThan(12);

  await component.update(<LiveDot state="reconnecting" />);

  await expect(dot).toHaveText('Reconnecting');
  expect((await dot.boundingBox())!.width).toBeGreaterThan(40);
});
