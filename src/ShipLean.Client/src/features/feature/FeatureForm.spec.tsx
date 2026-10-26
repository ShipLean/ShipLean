import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { priya, story } from '../workItem/ct/fixtures';
import { workItemHarnessIds as harness } from '../workItem/ct/harnessIds';
import { stubApi } from '../workItem/ct/stubApi';
import { stubComments } from '../workItem/ct/stubDetails';
import { stubSide } from '../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../workItem/ct/WorkItemFormHarness';
import { workItemTestIds } from '../workItem/testIds';
import { feature, stubFeature } from './ct/stubFeature';
import { featureTestIds as ids } from './testIds';

const route = (key: string) => ({ hooksConfig: { route: `/p/SHIP/items/${key}` } });

// The page as each stub sees it.
type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

// Every page.route the Feature form's regions call, answered from the fixtures.
async function stubAll(page: StubbedPage, item = feature) {
  const api = await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  return api;
}

// A description whose table is wider than the main column at any viewport.
const wideTableHtml = `<table><tbody><tr>${Array.from(
  { length: 12 },
  (_, i) => `<td><code>column_${i}_with_a_long_name</code></td>`,
).join('')}</tr></tbody></table>`;

// The test ids in document order, among the ones given.
const inDocumentOrder = (testIds: string[]) =>
  testIds.map((testId) => `[data-testid="${testId}"]`).join(', ');

test('FeatureForm_ShouldRenderRegionsInOrder', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route('SHIP-100'));

  const main = [
    ids.strip,
    ids.statusBand,
    ids.goal,
    workItemTestIds.description,
    ids.links,
    ids.storiesAndBugs,
    ids.log,
  ];
  const rail = [ids.attachmentsRail, workItemTestIds.history];
  for (const testId of [...main, ...rail]) {
    await expect(screen.getByTestId(testId)).toBeVisible();
  }
  await expect(screen.getByTestId(workItemTestIds.discussion)).toBeVisible();
  await expect(screen.getByTestId(workItemTestIds.attachments)).toBeVisible();

  const order = await page
    .locator(inDocumentOrder([...main, ...rail]))
    .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('data-testid')));
  expect(order).toEqual([...main, ...rail]);

  const zone = screen.getByTestId(ids.dropZone);
  await expect(zone.getByTestId(ids.form)).toBeVisible();
  await expect(zone.getByTestId(workItemTestIds.title)).toHaveValue('Checkout v2');
  await expect(screen.getByTestId(ids.main).getByTestId(ids.goal)).toBeVisible();
  await expect(screen.getByTestId(ids.links).getByTestId(ids.addLink)).toBeAttached();
  await expect(screen.getByTestId(ids.rail).getByTestId(ids.attachmentsRail)).toBeVisible();
  for (const marker of [harness.links, harness.tasks, harness.discussion, harness.side]) {
    await expect(screen.getByTestId(marker)).toHaveCount(0);
  }
});

test('FeatureForm_WhenDescriptionHasAWideTable_ShouldKeepItBesideTheRail', async ({
  mount,
  page,
}) => {
  await stubAll(page, { ...feature, descriptionHtml: wideTableHtml });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route('SHIP-100'));

  const description = screen.getByTestId(workItemTestIds.descriptionPreview);
  await expect(description.getByTestId(componentTestIds.markdown)).toBeVisible();
  const main = (await screen.getByTestId(ids.main).boundingBox())!;
  const shown = (await description.boundingBox())!;
  const rail = (await screen.getByTestId(ids.rail).boundingBox())!;
  expect(shown.x + shown.width).toBeLessThanOrEqual(main.x + main.width);
  expect(main.x + main.width).toBeLessThanOrEqual(rail.x);
});

test('StoryForm_ShouldStayUnchanged', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));

  await expect(screen.getByTestId(workItemTestIds.criteria)).toBeVisible();
  for (const marker of [harness.links, harness.tasks, harness.discussion, harness.side]) {
    await expect(screen.getByTestId(marker)).toBeAttached();
  }
  await expect(screen.getByTestId(ids.form)).toHaveCount(0);
  await expect(screen.getByTestId(ids.dropZone)).toHaveCount(0);
});

test('FeatureForm_WhenChildClosedWithoutRevisionBump_ShouldPassLiveItemToRegions', async ({
  mount,
  page,
}) => {
  const api = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route('SHIP-100'));
  const child = screen.getByTestId(ids.childRow).filter({ hasText: 'SHIP-141' });
  await expect(child.getByTestId(ids.childState)).toHaveText('Active');

  api.setCurrent({
    ...feature,
    children: feature.children.map((c) => ({
      ...c,
      state: { id: 's-done', name: 'Done', category: 'completed' as const },
    })),
  });
  await screen.getByTestId(harness.refetch).click();

  await expect(child.getByTestId(ids.childState)).toHaveText('Done');
  await expect(screen.getByTestId(workItemTestIds.saveState)).toHaveText(
    `Saved · revision ${feature.revision}`,
  );
});

test('FeatureForm_WhenSaveRefusedOnAField_ShouldStillShowTheSaveError', async ({ mount, page }) => {
  await stubApi(page, feature, () => ({
    status: 400,
    body: { error: 'A goal is at most 1,000 characters.', code: 'validation', field: 'goal' },
  }));
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route('SHIP-100'));

  await screen.getByTestId(workItemTestIds.title).fill('Checkout v3');
  await screen.getByTestId(workItemTestIds.save).click();

  await expect(screen.getByTestId(workItemTestIds.saveError)).toHaveText(
    'A goal is at most 1,000 characters.',
  );
});
