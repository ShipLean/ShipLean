import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { RetroFeaturesDto } from '../../../api/retros';
import { stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { featureRow, sampleFeatures } from './ct/featureRows';
import { RetroFeaturesPanel } from './RetroFeaturesPanel';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const route = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/features${query}` } });

async function serve(page: Parameters<typeof stubRetros>[0], data: RetroFeaturesDto) {
  await stubRetros(page);
  await page.route(/\/api\/projects\/[^/]+\/retros\/features$/, (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) }),
  );
}

test('columns follow the fixed order', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(4);
  await expect(page.locator('thead th')).toHaveText([
    'Feature',
    'State',
    'Retros',
    'Went well',
    'Not in the plan',
    'Went badly',
    'Can be improved',
    'To decide',
    'Decided',
    'Actions',
    'Last retro',
  ]);
  const saved = page.getByTestId(ids.retroFeaturesRow).filter({ hasText: 'SHIP-180' });
  await expect(saved.locator('td').nth(2)).toHaveText('3');
  await expect(saved.locator('td').nth(3)).toHaveText('4');
  await expect(saved.locator('td').nth(4)).toHaveText('1');
  await expect(saved.locator('td').nth(5)).toHaveText('2');
  await expect(saved.locator('td').nth(6)).toHaveText('3');
  await expect(saved.locator('td').nth(7)).toHaveText('2');
  await expect(saved.locator('td').nth(8)).toHaveText('3/5');
  await expect(page.getByText('Stories and bugs with no Feature')).toBeVisible();
});

test('To decide filter', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await page
    .getByTestId(ids.retroFeaturesFilter)
    .getByRole('button', { name: 'To decide' })
    .click();
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.retroFeaturesRow).filter({ hasText: 'SHIP-181' })).toHaveCount(
    0,
  );
  await expect(page.getByText('2 Features with retros')).toBeVisible();
});

test('To decide filter opens from the address', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route('?show=open'));
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(3);
  await expect(
    page.getByTestId(ids.retroFeaturesFilter).getByRole('button', { name: 'To decide' }),
  ).toHaveAttribute('aria-pressed', 'true');
});

test('No Feature retro yet filter', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await expect(page.getByText('No Feature retro yet', { exact: true })).toHaveCount(2);
  await page
    .getByTestId(ids.retroFeaturesFilter)
    .getByRole('button', { name: 'No Feature retro yet' })
    .click();
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.retroFeaturesRow)).toContainText('SHIP-181');
  await expect(page.getByText('Stories and bugs with no Feature')).toHaveCount(0);
});

test('search narrows rows', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await page.getByTestId(ids.retroFeaturesSearch).fill('mobile');
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.retroFeaturesRow)).toContainText('Mobile sign-in');
  await page.getByTestId(ids.retroFeaturesSearch).fill('SHIP-199');
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(1);
  await page.getByTestId(ids.retroFeaturesSearch).fill('zzz');
  await expect(page.getByText('Nothing here.')).toBeVisible();
});

test('a row links to the item page', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  await expect(page.getByRole('link', { name: 'Saved cards' })).toHaveAttribute(
    'href',
    '/p/SHIP/retros/items/SHIP-180',
  );
});

test('a loose bug row links to its item page', async ({ mount, page }) => {
  await serve(page, sampleFeatures);
  await mount(<RetroFeaturesPanel project={project} />, route());
  const bug = page.getByTestId(ids.retroFeaturesRow).filter({ hasText: 'SHIP-199' });
  await expect(bug.getByRole('link', { name: 'Favicon 404' })).toHaveAttribute(
    'href',
    '/p/SHIP/retros/items/SHIP-199',
  );
});

test('an empty project says there is nothing here', async ({ mount, page }) => {
  await serve(page, { features: [], loose: [] });
  await mount(<RetroFeaturesPanel project={project} />, route());
  await expect(page.getByText('Nothing here.')).toBeVisible();
  await expect(page.getByText('0 Features with retros')).toBeVisible();
});

test('60 rows all render', async ({ mount, page }) => {
  const features = Array.from({ length: 60 }, (_, i) =>
    featureRow({ id: `f${i}`, key: `SHIP-${300 + i}`, title: `Feature ${i}`, toDecide: i % 3 }),
  );
  await serve(page, { features, loose: [] });
  await mount(<RetroFeaturesPanel project={project} />, route());
  await expect(page.getByTestId(ids.retroFeaturesRow)).toHaveCount(60);
});
