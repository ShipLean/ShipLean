import { expect, test } from '@playwright/experimental-ct-react';
import { feature, linkCards, stubFeature } from '../../features/feature/ct/stubFeature';
import { featureTestIds as ids } from '../../features/feature/testIds';
import { priya } from '../../features/workItem/ct/fixtures';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubComments } from '../../features/workItem/ct/stubDetails';
import { attachment, stubSide } from '../../features/workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../features/workItem/ct/WorkItemFormHarness';
import type { Page } from './shoot';

// The Feature's links, Add link form and attachment rows on a phone (SHIP-714, L714f), as element shots of this task's
// regions only, so the other 714 tasks' merges don't move them.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/featureLinks.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

const attached = [
  { ...attachment('m1', 'design-v0.2.md', priya, 14336), kind: 'markdown' as const },
  { ...attachment('i1', 'checkout-funnel.png', priya, 90112), kind: 'image' as const },
  { ...attachment('f1', 'funnel-oct.csv', priya, 31744), kind: 'file' as const },
];
const settled = { ...linkCards, pages: linkCards.pages.filter((card) => !card.refreshDue) };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function arrange(page: StubbedPage) {
  await stubApi(page, feature);
  await stubSide(page, priya, attached, []);
  await stubComments(page, priya, []);
  await stubFeature(page, { cards: settled });
}

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

const regions = [
  { name: 'feature-links', region: ids.links, addLink: false },
  { name: 'feature-add-link', region: ids.addLink, addLink: true },
  { name: 'feature-attachments', region: ids.attachmentsRail, addLink: false },
];

for (const theme of THEMES) {
  for (const { name, region, addLink } of regions) {
    test(`FeatureLinksPhone_WhenOpened_ShouldMatchItsScreenshot_${name}_${theme}`, async ({
      mount,
      page,
    }) => {
      phoneOnly(page);
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await arrange(page);
      const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
      await expect(screen.getByTestId(ids.linkCard)).toHaveCount(4);
      await expect(screen.getByTestId(ids.railItem)).toHaveCount(3);
      if (addLink) {
        await screen.getByTestId(ids.addLinkOpen).tap();
        await expect(screen.getByTestId(ids.addLinkForm)).toBeVisible();
      }

      await expect(screen.getByTestId(region)).toHaveScreenshot(`${name}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}
