import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RetroFeaturesDto } from '@shiplean/core/api/retros';
import { stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { featureRow, sampleFeatures } from './ct/featureRows';
import { RetroFeaturesPanel } from './RetroFeaturesPanel';

// Retrospectives › Features on a phone (SHIP-728): under 600px one list of row links with a legend, and a one-row toolbar
// with Search; at 768px the desktop table at touch sizes with the Feature column pinned. Regenerate the baselines with
// scripts/update-page-shots.sh src/features/retros/features/RetroFeatures.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const route = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/features${query}` } });

type Page = Parameters<typeof stubRetros>[0];

async function serve(page: Page, data: RetroFeaturesDto) {
  await stubRetros(page);
  await page.route(/\/api\/projects\/[^/]+\/retros\/features$/, (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) }),
  );
}

const nothingToDecide: RetroFeaturesDto = {
  features: [featureRow({ id: 'f1', key: 'SHIP-180', title: 'Saved cards', toDecide: 0 })],
  loose: [],
};

for (const theme of THEMES) {
  test(`RetroFeaturesPhone_WhenOpened_ShouldMatchItsScreenshot_retro-features-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page, sampleFeatures);
    const screen = await mount(<RetroFeaturesPanel project={project} />, route());
    await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(4);
    await expect(screen).toHaveScreenshot(`retro-features-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`RetroFeaturesPhone_WhenSearchOpened_ShouldMatchItsScreenshot_retro-features-phone-search_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page, sampleFeatures);
    const screen = await mount(<RetroFeaturesPanel project={project} />, route());
    await page.getByTestId(ids.retroFeaturesSearchOpen).tap();
    await page.getByTestId(ids.retroFeaturesSearch).fill('saved');
    await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(1);
    await expect(screen).toHaveScreenshot(`retro-features-phone-search.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`RetroFeaturesPhone_WhenNothingToDecide_ShouldMatchItsScreenshot_retro-features-phone-empty_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page, nothingToDecide);
    const screen = await mount(<RetroFeaturesPanel project={project} />, route('?show=open'));
    await expect(page.getByTestId(ids.retroFeaturesEmpty)).toHaveText('Nothing left to decide.');
    await expect(screen).toHaveScreenshot(`retro-features-phone-empty.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`RetroFeaturesTablet_WhenOpenedAt768_ShouldMatchItsScreenshot_retro-features-tablet_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page, sampleFeatures);
    const screen = await mount(<RetroFeaturesPanel project={project} />, route());
    await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(4);
    await expect(screen).toHaveScreenshot(`retro-features-tablet.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('RetroFeaturesPhone_WhenOpened_ShouldShowRowLinksAndTheLegendInOrder', async ({
  mount,
  page,
}) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await expect(page.getByTestId(ids.retroFeaturesTableBox)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroFeaturesCount)).toHaveText('3 Features with retros');
  await expect(page.getByTestId(ids.retroFeaturesLegendItem)).toHaveText([
    'Went well',
    'Not in the plan',
    'Went badly',
    'Can be improved',
  ]);
  const row = page.getByTestId(ids.retroFeaturesRow).filter({ hasText: 'SHIP-181' });
  await expect(row).toHaveAttribute('href', '/p/SHIP/retros/items/SHIP-181');
  await expect(row.getByTestId(ids.retroFeaturesNoFeatureChip)).toBeVisible();
  await expect(row.getByTestId(ids.retroFeaturesMeta)).toHaveText('3 retros · 2 action items');
  await expect(
    page
      .getByTestId(ids.retroFeaturesRow)
      .filter({ hasText: 'SHIP-180' })
      .getByTestId(ids.retroFeaturesToDecide),
  ).toHaveText('2 to decide');
  await expect(page.getByTestId(ids.retroFeaturesLoose)).toBeVisible();
});

test('RetroFeaturesPhone_WhenSearchCancelled_ShouldClearTheSearch', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await page.getByTestId(ids.retroFeaturesSearchOpen).tap();
  const field = page.getByTestId(ids.retroFeaturesSearch);
  await expect(field).toBeFocused();
  await field.fill('zzz');
  await expect(page.getByTestId(ids.retroFeaturesEmpty)).toContainText('No Feature matches “zzz”.');
  await page.getByTestId(ids.retroFeaturesClearSearch).tap();
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(4);
  await expect(field).toHaveValue('');
  await field.fill('saved');
  await page.getByTestId(ids.retroFeaturesSearchCancel).tap();
  await expect(page.getByTestId(ids.retroFeaturesSearch)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(4);
  await expect(page.getByTestId(ids.retroFeaturesSearchOpen)).toHaveAttribute(
    'aria-pressed',
    'false',
  );
});

test('RetroFeaturesTablet_WhenTableScrolled_ShouldKeepTheFeatureColumnInPlace', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await expect(page.getByTestId(ids.retroFeaturesSearchOpen)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroFeaturesPhoneList)).toHaveCount(0);
  const box = page.getByTestId(ids.retroFeaturesTableBox);
  const cell = page.getByTestId(ids.retroFeaturesFeatureCell).first();
  const before = (await cell.boundingBox())!.x;
  await box.evaluate((el) => {
    el.scrollLeft = 120;
  });
  expect(await box.evaluate((el) => el.scrollLeft)).toBeGreaterThan(0);
  expect((await cell.boundingBox())!.x).toBeCloseTo(before, 0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
  expect(
    (await page.getByTestId(ids.retroFeaturesRow).first().boundingBox())!.height,
  ).toBeGreaterThanOrEqual(48);
});
