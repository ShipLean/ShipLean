import { expect, test } from '@playwright/experimental-ct-react';
import type { StateDto } from '@shiplean/core/api/projects';
import { feature, stubFeature } from '../../features/feature/ct/stubFeature';
import { featureTestIds as ids } from '../../features/feature/testIds';
import { priya } from '../../features/workItem/ct/fixtures';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubComments } from '../../features/workItem/ct/stubDetails';
import { stubSide } from '../../features/workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../features/workItem/ct/WorkItemFormHarness';
import type { Page } from './shoot';

// The Feature's status band, status editor, fields and Stories and bugs on a phone (SHIP-714, L714e), as element shots of
// this task's regions only, so the other 714 tasks' merges don't move them.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/featureTop.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

const states: StateDto[] = [
  { id: 's-idea', type: 'feature', name: 'Idea', category: 'proposed', order: 1, itemCount: 0 },
  {
    id: 's-active',
    type: 'feature',
    name: 'Active',
    category: 'inProgress',
    order: 2,
    itemCount: 1,
  },
  {
    id: 's-blocked',
    type: 'feature',
    name: 'Blocked',
    category: 'inProgress',
    order: 3,
    itemCount: 0,
  },
];

const epic = {
  id: 'e1',
  key: 'SHIP-90',
  type: { id: 'epic' as const, name: 'Epic' },
  title: 'Checkout conversion',
};
const child = (key: string, title: string, bug = false) => ({
  ...feature.children[0],
  id: `c-${key}`,
  key,
  title,
  type: bug ? { id: 'bug' as const, name: 'Bug' } : feature.children[0].type,
});
const item = {
  ...feature,
  parent: epic,
  slug: 'checkout-v2',
  startedLabel: 'Mon 21 Sep',
  tags: ['Checkout', 'PCI'],
  statusLine: 'Saved cards are in review; the vault rate limit is the open risk.',
  children: [
    child('SHIP-141', 'Pay with a saved card from the review step without re-entering the code'),
    child('SHIP-142', 'Guest checkout keeps the cart'),
    child('SHIP-143', 'Card form loses focus on iOS', true),
  ],
};

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function arrange(page: StubbedPage) {
  await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  await page.route('**/api/projects/p1/states', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(states) }),
  );
}

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

const regions = [
  { name: 'feature-band', region: ids.statusBand, edit: false },
  { name: 'feature-status-edit', region: ids.statusBand, edit: true },
  { name: 'feature-fields', region: ids.strip, edit: false },
  { name: 'feature-children', region: ids.storiesAndBugs, edit: false },
];

for (const theme of THEMES) {
  for (const { name, region, edit } of regions) {
    test(`FeatureTopPhone_WhenOpened_ShouldMatchItsScreenshot_${name}_${theme}`, async ({
      mount,
      page,
    }) => {
      phoneOnly(page);
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await arrange(page);
      const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
      await expect(screen.getByTestId(ids.statusBand)).toBeVisible();
      await expect(screen.getByTestId(ids.childRow)).toHaveCount(3);
      if (edit) {
        await screen.getByTestId(ids.statusEdit).tap();
        await expect(screen.getByTestId(ids.statusEditor)).toBeVisible();
      }

      await expect(screen.getByTestId(region)).toHaveScreenshot(`${name}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}
