import { expect, test } from '@playwright/experimental-ct-react';
import type { CommentDto } from '@shiplean/core/api/comments';
import type { StateDto } from '@shiplean/core/api/projects';
import { feature, stubFeature } from '../../features/feature/ct/stubFeature';
import { featureTestIds as ids } from '../../features/feature/testIds';
import { priya } from '../../features/workItem/ct/fixtures';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubComments } from '../../features/workItem/ct/stubDetails';
import { stubSide } from '../../features/workItem/ct/stubSide';
import { workItemTestIds } from '../../features/workItem/testIds';
import { WorkItemFormHarness } from '../../features/workItem/ct/WorkItemFormHarness';
import { log } from './fixtures/feature';
import { PEOPLE } from './shellStub';
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

// The profile photos the fixture people carry (the Feature fixture's assignee), so the avatars draw instead of breaking.
const photo =
  '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="64" height="64" fill="#2f7fd8"/></svg>';

async function arrange(page: StubbedPage, shown = item, comments: CommentDto[] = []) {
  await stubApi(page, shown);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, comments);
  await stubFeature(page);
  await page.route('**/api/users/*/photo*', (r) =>
    r.fulfill({ status: 200, contentType: 'image/svg+xml', body: photo }),
  );
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

// A wide table, a long code line and an attached image in the Feature's own description, on the phone.
const wideTable = `<table><thead><tr>${['Tier', 'Rate limit', 'Burst', 'Owner', 'Environment', 'Notes'].map((h) => `<th>${h}</th>`).join('')}</tr></thead><tbody><tr>${['Staging', '50 a minute', '10 a second', 'Vault team', 'staging-eu-west', 'Raised for the picker launch'].map((c) => `<td>${c}</td>`).join('')}</tr></tbody></table>`;
const wideCode = `<pre><code>${'GET /cards?customer=c_1234&amp;include=brand,last4,expiry '.repeat(3)}</code></pre>`;
const picture = `data:image/svg+xml;utf8,${encodeURIComponent(
  '<svg xmlns="http://www.w3.org/2000/svg" width="600" height="200"><rect width="100%" height="100%" fill="#3b6ea8"/><text x="20" y="40" font-size="28" fill="white">Checkout flow</text></svg>',
)}`;
const withDescription = {
  ...item,
  description: 'Rebuild checkout around **saved cards**.',
  descriptionHtml: `<p>Rebuild checkout around <strong>saved cards</strong> and a single review step.</p>${wideTable}${wideCode}<figure data-md-image="attached"><img src="${picture}" alt="Checkout flow"><figcaption>checkout-flow.svg</figcaption></figure>`,
};
const atRisk = {
  ...item,
  health: 'atRisk' as const,
  healthSet: { at: '2026-10-24T09:12:00+00:00', atLabel: '24 Oct', by: PEOPLE.priya, agent: null },
};

for (const theme of THEMES) {
  test(`FeatureTopPhone_WhenHealthAtRisk_ShouldMatchItsScreenshot_feature-band-at-risk_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await arrange(page, atRisk);
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
    await expect(screen.getByTestId(ids.statusHealth)).toHaveText('At risk');
    await expect(screen.getByTestId(ids.statusHealthSetBy)).toBeVisible();

    await expect(screen.getByTestId(ids.statusBand)).toHaveScreenshot(
      `feature-band-at-risk.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`FeatureTopPhone_WhenDescriptionHasTableCodeAndImage_ShouldMatchItsScreenshot_feature-description_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await arrange(page, withDescription);
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
    const description = screen.getByTestId(workItemTestIds.description);
    await expect(description.locator('table')).toBeVisible();
    await expect(description.locator('img')).toHaveJSProperty('complete', true);

    await expect(description).toHaveScreenshot(`feature-description.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`FeatureTopPhone_WhenLogHasAgentAndSupersededEntries_ShouldMatchItsScreenshot_feature-log_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await arrange(page, item, log);
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
    await expect(screen.getByTestId(ids.logCount)).toHaveText('3');

    await expect(screen.getByTestId(ids.log)).toHaveScreenshot(`feature-log.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
