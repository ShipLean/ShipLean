import { expect, test } from '@playwright/experimental-ct-react';
import { RegionHarness } from './ct/RegionHarness';
import { empty, feature, featureChildrenOnly } from './ct/fixtures';
import { WaitingChildren } from './WaitingChildren';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';

const item: AsksItem = {
  id: feature.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-101',
  type: { id: 'feature', name: 'Feature' },
};

test('WaitingChildren_WhenTwoChildrenWait_ShouldListBothWithCounts', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <WaitingChildren item={item} asks={feature} />
    </RegionHarness>,
  );

  const rows = component.getByTestId(ids.waitingChildRow);
  await expect(rows).toHaveCount(2);
  await expect(component).toContainText('Stories and bugs waiting');
  await expect(rows.nth(0)).toContainText('SHIP-141');
  await expect(rows.nth(0)).toContainText('Pay with saved card');
  await expect(rows.nth(0)).toContainText('Waiting on Joel Dickson');
  await expect(rows.nth(0)).toContainText('1 question');
  await expect(rows.nth(0)).toContainText('4 decisions open');
  await expect(rows.nth(0).getByRole('link', { name: 'SHIP-141' })).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-141',
  );
  await expect(rows.nth(1)).toContainText('2 questions');
  await expect(rows.nth(1)).not.toContainText('decision');
});

test('WaitingChildren_WhenUnassigned_ShouldNoteCreator', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <WaitingChildren item={item} asks={feature} />
    </RegionHarness>,
  );

  const rows = component.getByTestId(ids.waitingChildRow);
  await expect(rows.nth(1)).toContainText('Waiting on Tom Okafor (creator)');
  await expect(rows.nth(0)).not.toContainText('(creator)');
});

test('WaitingChildren_WhenNone_ShouldRenderNothing', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <WaitingChildren item={item} asks={{ ...empty, workItemId: item.id }} />
    </RegionHarness>,
  );

  await expect(component.getByTestId(ids.waitingChildRow)).toHaveCount(0);
  await expect(component).not.toContainText('Stories and bugs waiting');
});

for (const theme of ['light', 'dark'] as const) {
  test(`WaitingChildren_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const component = await mount(
      <RegionHarness>
        <WaitingChildren item={item} asks={featureChildrenOnly} />
      </RegionHarness>,
    );
    await expect(component.getByTestId(ids.waitingChildRow)).toHaveCount(2);
    await expect(component).toHaveScreenshot(`asks-waiting-children.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
