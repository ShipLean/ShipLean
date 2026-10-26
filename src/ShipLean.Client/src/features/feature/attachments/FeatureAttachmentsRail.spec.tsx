import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { attachment, stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { componentTestIds } from '../../../components/testIds';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';
import type { FeatureStubOptions } from '../ct/stubFeature';

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

const attached = [
  { ...attachment('m1', 'design-v0.2.md', priya, 14336), kind: 'markdown' as const },
  { ...attachment('i1', 'checkout-funnel.png', priya, 90112), kind: 'image' as const },
  { ...attachment('f1', 'funnel-oct.csv', priya, 31744), kind: 'file' as const },
];

async function stubAll(page: StubbedPage, options: FeatureStubOptions = {}) {
  await stubApi(page, feature);
  const side = await stubSide(page, priya, attached, []);
  await stubComments(page, priya, []);
  const stubbed = await stubFeature(page, options);
  return { side, ...stubbed };
}

const file = (name: string, size = 5) => ({
  name,
  mimeType: 'text/plain',
  buffer: Buffer.alloc(size, 'x'),
});

test('AttachmentsRail_ShouldGroupByKind', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  const rail = screen.getByTestId(ids.attachmentsRail);
  const markdown = rail.getByTestId(ids.railGroupMarkdown);
  await expect(markdown).toContainText('design-v0.2.md');
  await expect(markdown).toContainText('14 KB');
  await expect(rail.getByTestId(ids.railGroupImage)).toContainText('checkout-funnel.png');
  const files = rail.getByTestId(ids.railGroupFile);
  await expect(files.getByRole('link')).toHaveAttribute('href', '/api/attachments/f1/content');
  await expect(screen.getByTestId(ids.railCount)).toHaveText('3');
});

test('ImagePreview_WhenImageClicked_ShouldOpenDialog', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.railItem).filter({ hasText: 'checkout-funnel.png' }).click();

  await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
  await expect(page.getByTestId(ids.imagePreviewImage)).toHaveAttribute(
    'src',
    '/api/attachments/i1/content',
  );
  await page.getByTestId(ids.imagePreviewClose).click();
  await expect(page.getByTestId(ids.imagePreview)).toHaveCount(0);
});

test('DropOverlay_WhenNameExists_ShouldFlagReplace', async ({ mount, page }) => {
  await stubAll(page, {
    outcomes: { 'design-v0.2.md': { outcome: 'replace', message: 'Replaces the existing file.' } },
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.dropInput).setInputFiles([file('design-v0.2.md'), file('brief.md')]);

  const outcomes = page.getByTestId(ids.dropOutcome);
  await expect(outcomes).toHaveCount(2);
  await expect(outcomes.nth(0)).toHaveAttribute('data-outcome', 'replace');
  await expect(outcomes.nth(0)).toContainText('Replace');
  await expect(outcomes.nth(0).getByTestId(ids.dropOutcomeMessage)).toHaveText(
    'Replaces the existing file.',
  );
  await expect(outcomes.nth(1)).toHaveAttribute('data-outcome', 'add');
});

test('DropOverlay_WhenTooLarge_ShouldNotUpload', async ({ mount, page }) => {
  const { side } = await stubAll(page, {
    outcomes: { 'huge.bin': { outcome: 'tooLarge', message: 'Over 25 MB.' } },
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.dropInput).setInputFiles([file('huge.bin')]);

  await expect(page.getByTestId(ids.dropOutcome)).toContainText('Too large');
  await expect(page.getByTestId(ids.dropUpload)).toBeDisabled();
  expect(side.uploaded).toEqual([]);
});

test('DropOverlay_WhenOverItemLimit_ShouldFlagOverTotal', async ({ mount, page }) => {
  const { side } = await stubAll(page, {
    outcomes: {
      'late.zip': { outcome: 'overTotal', message: 'A work item can hold at most 250 MB.' },
    },
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.dropInput).setInputFiles([file('late.zip'), file('ok.txt')]);

  const outcomes = page.getByTestId(ids.dropOutcome);
  await expect(outcomes.nth(0)).toHaveAttribute('data-outcome', 'overTotal');
  await expect(outcomes.nth(0)).toContainText('Over the item limit');
  await page.getByTestId(ids.dropUpload).click();
  await expect(page.getByTestId(ids.dropOverlay)).toHaveCount(0);
  expect(side.uploaded).toEqual(['ok.txt']);
});

test('DropOverlay_WhenUploadFails_ShouldShowErrorPerFile', async ({ mount, page }) => {
  const { side } = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  side.refuseNext('That file type is not allowed.');

  await screen.getByTestId(ids.dropInput).setInputFiles([file('bad.exe'), file('fine.txt')]);
  await page.getByTestId(ids.dropUpload).click();

  const errors = page.getByTestId(ids.dropError);
  await expect(errors).toHaveCount(1);
  await expect(errors).toHaveText('That file type is not allowed.');
  await expect(page.getByTestId(ids.dropOutcome)).toHaveCount(1);
  expect(side.uploaded).toEqual(['fine.txt']);
});

test('AttachmentsRail_WhenRemoveConfirmed_ShouldArchiveAndDropTheRow', async ({ mount, page }) => {
  const { side } = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen
    .getByTestId(ids.railItem)
    .filter({ hasText: 'funnel-oct.csv' })
    .getByTestId(ids.railItemRemove)
    .click();

  await expect(screen.getByTestId(ids.railItem)).toHaveCount(2);
  await expect(screen.getByTestId(ids.railGroupFile)).toHaveCount(0);
  expect(side.archived).toEqual(['f1']);
  await expect(screen.getByTestId(componentTestIds.toast)).toContainText('Removed funnel-oct.csv');
});

test('AttachmentsRail_WhenAttachSameName_ShouldShowReplaceOutcome', async ({ mount, page }) => {
  await stubAll(page, {
    outcomes: { 'design-v0.2.md': { outcome: 'replace', message: 'Replaces the existing file.' } },
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const chooser = page.waitForEvent('filechooser');

  await screen.getByTestId(ids.railAttach).click();
  await (await chooser).setFiles(file('design-v0.2.md'));

  const outcome = page.getByTestId(ids.dropOutcome);
  await expect(outcome).toHaveAttribute('data-outcome', 'replace');
  await expect(outcome.getByTestId(ids.dropOutcomeMessage)).toHaveText(
    'Replaces the existing file.',
  );
});
