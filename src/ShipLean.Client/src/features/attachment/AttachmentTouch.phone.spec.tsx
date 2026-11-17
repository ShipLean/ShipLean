import { expect, test } from '@playwright/experimental-ct-react';
import { markdownView, stubFeature } from '../feature/ct/stubFeature';
import { touchTestIds } from '../../touch/testIds';
import { AttachmentHarness } from './ct/AttachmentHarness';
import { attachmentTestIds as ids } from './testIds';

// The Markdown attachment page's phone header, ⋯ sheet and Contents pill (SHIP-715, L715a) on the CT phone project.

const mountProps = { itemKey: markdownView.workItem.key, attachmentId: markdownView.attachment.id };

test('AttachmentMoreSheet_WhenCopyLinkTapped_ShouldCopyAndClose', async ({
  mount,
  page,
  context,
}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  await screen.getByTestId(touchTestIds.moreButton).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  const rows = sheet.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveCount(4);
  await expect(rows).toHaveText([/^Download/, /^Replace…/, /^Copy link/, /^About this file/]);

  await sheet.locator('[data-action-id="copy-link"]').tap();

  await expect(sheet).toHaveCount(0);
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(
    await page.evaluate(() => window.location.href),
  );
});

test('ContentsPill_WhenOutlineHasOneEntry_ShouldHide', async ({ mount, page }) => {
  await stubFeature(page, {
    view: { ...markdownView, outline: [markdownView.outline[0]!] },
  });
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  await expect(screen.getByTestId(ids.name)).toBeVisible();

  await expect(page.getByTestId(ids.contentsPill)).toHaveCount(0);
});

test('ContentsPill_WhenOutlineHasTwoEntries_ShouldNameTheHeadingAndListTheOutline', async ({
  mount,
  page,
}) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  const pill = page.getByTestId(ids.contentsPill);
  await expect(pill).toContainText('Vault notes');

  await pill.tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('On this page');
  await expect(sheet.getByTestId(ids.contentsEntry)).toHaveText(['Vault notes', 'Tokens']);
  await sheet.getByTestId(ids.contentsEntry).last().tap();
  await expect(sheet).toHaveCount(0);
  await expect(screen.getByTestId(ids.body)).toBeVisible();
});

test('ContentsPill_WhenViewIsSource_ShouldHide', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  await expect(page.getByTestId(ids.contentsPill)).toBeVisible();

  await screen.getByTestId(ids.source).tap();

  await expect(page.getByTestId(ids.contentsPill)).toHaveCount(0);
});

test('AttachmentHeader_WhenFileNameTapped_ShouldOpenSiblingsSheet', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  await expect(screen.getByTestId(ids.back)).toBeHidden();

  await screen.getByTestId(ids.nameButton).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Markdown on SHIP-141');
  await expect(sheet.getByTestId(ids.sibling)).toHaveText([/vault-notes\.md/, /rollout-plan\.md/]);
  const row = await sheet.getByTestId(ids.sibling).first().boundingBox();
  expect(row?.height).toBeGreaterThanOrEqual(44);
});

test('AttachmentHeader_WhenFactsTapped_ShouldOpenAboutSheet', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await screen.getByTestId(ids.facts).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('About this file');
  await expect(sheet.getByTestId(ids.imageRow)).toHaveCount(1);
  await expect(sheet.getByTestId(ids.noHistory)).toBeVisible();
  await expect(sheet).toContainText('replaced twice');
});

test('AttachmentMenu_ShouldShowIconsAndSubLines', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  const more = screen.getByTestId(touchTestIds.moreButton);
  await more.tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  const rows = sheet.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveCount(4);
  // An icon slot on every row, the size under Download and the soft-delete note under Replace…
  await expect(rows.locator('svg')).toHaveCount(4);
  await expect(sheet.getByTestId(touchTestIds.actionSheetHint)).toHaveText([
    /\d+ (B|KB|MB)/,
    'Pick a file; the old copy is soft-deleted',
  ]);
  // A divider before About this file.
  await expect(sheet.locator('hr')).toHaveCount(1);
  await expect(more).toHaveClass(/border-edge/);
});

test('AttachmentAbout_ShouldListFactsAsRows', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await screen.getByTestId(ids.facts).tap();

  const facts = page.getByTestId(touchTestIds.sheet).getByTestId(ids.aboutFact);
  await expect(facts).toHaveCount(4);
  await expect(facts.locator('dt')).toHaveText(['Size', 'Uploaded by', 'When', 'On']);
  await expect(facts.nth(2)).toContainText('replaced twice');
  await expect(facts.nth(3)).toContainText(markdownView.workItem.key);
});

test('AttachmentAbout_WhenNoImageMissing_ShouldNotShowTheHint', async ({ mount, page }) => {
  await stubFeature(page, {
    view: {
      ...markdownView,
      images: [{ src: 'flow.svg', status: 'attached', attachmentId: 'a-flow' }],
    },
  });
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await screen.getByTestId(ids.facts).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(ids.imageRow)).toHaveCount(1);
  await expect(sheet.getByTestId(ids.imagesMissingHint)).toHaveCount(0);
});

test('AttachmentAbout_WhenAnImageIsMissing_ShouldShowTheHint', async ({ mount, page }) => {
  await stubFeature(page, {
    view: {
      ...markdownView,
      images: [
        { src: 'flow.svg', status: 'attached', attachmentId: 'a-flow' },
        { src: 'https://i.imgur.com/x.png', status: 'external', attachmentId: null },
      ],
    },
  });
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await screen.getByTestId(ids.facts).tap();

  await expect(page.getByTestId(ids.imagesMissingHint)).toContainText('Attach the missing image');
});

test('AttachmentHeader_ShouldStretchTheToggleAndBorderTheMoreButton', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  const header = screen.getByTestId(ids.header);
  await expect(header).toContainText('MD');

  const toggle = await header.getByRole('group', { name: 'View' }).boundingBox();
  const more = await screen.getByTestId(touchTestIds.moreButton).boundingBox();
  const headerBox = await header.boundingBox();
  // The toggle takes the row up to the ⋯, so the two together span the header's width less its padding.
  expect((toggle?.width ?? 0) + (more?.width ?? 0)).toBeGreaterThan((headerBox?.width ?? 0) - 40);
  await expect(page.getByTestId(ids.contentsPosition)).toHaveText('1 / 2');
});

test('AttachmentTooLarge_ShouldShowTheFileHeaderAndDownloadWithSize', async ({ mount, page }) => {
  const stub = await stubFeature(page);
  await page.route('**/api/projects/p1/work-items/by-key/*', (route) =>
    route.fulfill({ json: { id: markdownView.workItem.id } }),
  );
  await page.route('**/api/work-items/*/attachments', (route) =>
    route.fulfill({
      json: [{ ...markdownView.attachment, name: 'design-v0.2.md', sizeBytes: 2.4 * 1024 * 1024 }],
    }),
  );
  stub.failNext('markdownView', 400, {
    error: 'This file is too large to show.',
    code: 'markdown_too_large',
  });
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await expect(screen.getByTestId(ids.name)).toHaveText('design-v0.2.md');
  await expect(screen.getByTestId(ids.tooLargeHeading)).toHaveText('Too large to show here');
  const download = screen.getByTestId(ids.loadFailedDownload);
  await expect(download).toHaveText('Download (2.4 MB)');
  await expect(download).toHaveClass(/btn-signal/);
});
