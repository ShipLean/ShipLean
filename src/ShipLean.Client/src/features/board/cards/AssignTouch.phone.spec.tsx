import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { BoardHarness } from '../ct/BoardHarness';
import { CardActionsOpenHarness } from '../ct/CardActionsOpenHarness';
import { TOM, featuresBoard, rollupBoard, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';
import { assignChildren, assignMembers } from './assignTouchFixtures';

// Assign / Owner and the children list on a phone (SHIP-719, L719e). Runs in the CT phone project.
// Regenerate shots with scripts/update-page-shots.sh src/pages/screenshots/boardAssign.phone.spec.tsx.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
const TOUCH = 32;
const stories = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const features = { hooksConfig: { route: '/p/SHIP/boards/features' } };

const sheet = (page: Page) => page.getByTestId(touchTestIds.sheet);
const row = (page: Page, id: string) =>
  sheet(page).locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`);

test('CardActionsSheet_WhenFeature_ShouldSayOwner', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), { level: 'features', members: assignMembers });
  await mount(<BoardHarness level="features" />, features);

  await page
    .getByTestId(ids.card)
    .filter({ hasText: 'SHIP-104' })
    .getByTestId(touchTestIds.moreButton)
    .tap();
  await expect(row(page, 'assign')).toHaveText('Owner');
  await row(page, 'assign').tap();
  await expect(sheet(page).getByTestId(touchTestIds.sheetTitle)).toHaveText('Owner');
  await expect(sheet(page).getByTestId(ids.assignOption).first()).toHaveText('No owner');
});

test('CardActionsSheet_WhenStory_ShouldSayAssign', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard(), { members: assignMembers });
  await mount(<BoardHarness />, stories);

  await page
    .getByTestId(ids.card)
    .filter({ hasText: 'SHIP-160' })
    .getByTestId(touchTestIds.moreButton)
    .tap();
  await expect(row(page, 'assign')).toHaveText('Assign');
  await row(page, 'assign').tap();
  await expect(sheet(page).getByTestId(ids.assignOption)).toHaveText([
    'Unassigned',
    ...assignMembers.map((member) => member.name),
  ]);
});

test('CardActionsSheet_WhenLongPressOpened_ShouldOfferAssign', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), { level: 'features', members: assignMembers });
  await mount(<CardActionsOpenHarness view={featuresBoard()} cardId="id-SHIP-104" />, features);

  await page.getByTestId('harness-open').tap();
  await expect(row(page, 'assign')).toHaveText('Owner');
});

test('ChildList_WhenTouch_ShouldUse32pxRows', async ({ mount, page }) => {
  await stubBoard(page, rollupBoard(), {
    level: 'features',
    children: () => assignChildren,
  });
  await mount(<BoardHarness level="features" />, features);

  await page
    .getByTestId(ids.card)
    .filter({ hasText: 'SHIP-101' })
    .getByTestId(touchTestIds.moreButton)
    .tap();
  await row(page, 'below').tap();
  const list = page.getByTestId(ids.childList);
  await expect(list.getByTestId(ids.childRow)).toHaveCount(assignChildren.length);

  const targets = list.locator(
    `[data-testid="${ids.childRow}"], [data-testid="${ids.childAdd}"], [data-testid="${ids.childClose}"]`,
  );
  const count = await targets.count();
  expect(count).toBeGreaterThan(assignChildren.length);
  for (let i = 0; i < count; i += 1) {
    const box = (await targets.nth(i).boundingBox())!;
    expect(box.height, `target ${i}`).toBeGreaterThanOrEqual(TOUCH);
  }
  await list.getByTestId(ids.childAdd).first().tap();
  await expect(list.getByTestId(ids.childAddTitle)).toHaveCSS('font-size', '16px');
});

test('Assign_WhenPicked_ShouldPatchTheItemWithItsRevision', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard(), { members: assignMembers });
  const bodies: unknown[] = [];
  await page.route('**/api/work-items/id-SHIP-160', (route) => {
    bodies.push(route.request().postDataJSON());
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'id-SHIP-160',
        key: 'SHIP-160',
        revision: 2,
        changes: [],
        notices: [],
      }),
    });
  });
  await mount(<BoardHarness />, stories);

  const card = page.getByTestId(ids.card).filter({ hasText: 'SHIP-160' });
  await card.getByTestId(touchTestIds.moreButton).tap();
  await row(page, 'assign').tap();
  await sheet(page).getByTestId(ids.assignOption).filter({ hasText: TOM.name }).tap();

  await expect.poll(() => bodies.length).toBe(1);
  expect(bodies[0]).toMatchObject({
    expectedRevision: 1,
    changes: { assignee: { value: TOM.id }, state: null, sprint: null },
  });
  await expect(sheet(page)).toHaveCount(0);
});
