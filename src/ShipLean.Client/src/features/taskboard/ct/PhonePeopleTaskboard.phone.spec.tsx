import { expect, test } from '@playwright/experimental-ct-react';
import type { MemberDto } from '@shiplean/core/api/projects';
import type { UserRef } from '@shiplean/core/api/refs';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { taskboardTestIds as ids } from '../testIds';
import { PRIYA, PROJECT, SPRINT, SPRINTS, TOM, VIEW } from './fixtures';
import { stubTaskboard } from './stubApi';

// The sprint Taskboard grouped by People on a phone (SHIP-725 D16): everyone on the project is a lane. Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const slot = <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />;
const THEMES = ['light', 'dark'] as const;

const MEI: UserRef = {
  id: 'u-mei',
  name: 'Mei Lin',
  initials: 'ML',
  avatarColour: '#8a5ab5',
  photoUrl: null,
};
const member = (user: UserRef): MemberDto => ({
  user,
  email: `${user.id}@example.com`,
  role: 'member',
  status: 'active',
});
const MEMBERS = [PRIYA, TOM, MEI].map(member);

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

async function arrange(page: Page) {
  await page.route(`**/api/projects/${PROJECT.id}/members`, (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MEMBERS) }),
  );
  await stubTaskboard(page, VIEW, { saved: { groupBy: 'people' }, holdLaterReads: true });
}

test('PhonePeople_WhenPersonHasNoTasks_ShouldShowEmptyLane', async ({ mount, page }) => {
  await arrange(page);
  await mount(slot, route);

  const first = page.getByTestId('touch-pager-page').first();
  const lanes = first.getByTestId(ids.lane);
  await expect(lanes).toHaveCount(4);
  await expect(lanes.first()).toHaveAttribute('data-row', 'unassigned');
  const mei = lanes.filter({ hasText: 'Mei Lin' });
  await expect(mei.getByTestId(ids.phonePersonEmpty)).toHaveText('No tasks in this sprint');
  await expect(mei.getByTestId(ids.rowCount)).toHaveText('0 tasks');
  // Priya has tasks, only none on this page: the usual None.
  await expect(lanes.filter({ hasText: 'Priya Shah' }).getByTestId(ids.laneNone)).toHaveText(
    'None',
  );
});

for (const theme of THEMES) {
  test(`PhonePeople_WhenEveryoneShown_ShouldMatchTheScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await arrange(page);
    await mount(slot, route);

    const first = page.getByTestId('touch-pager-page').first();
    await expect(first.getByTestId(ids.phonePersonEmpty)).toBeVisible();
    await expect(first).toHaveScreenshot(`taskboard-people-phone-${theme}.png`);
  });
}
