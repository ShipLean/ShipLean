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
  await expect(sheet.getByTestId(touchTestIds.actionSheetRow)).toHaveText([
    'Download',
    'Replace…',
    'Copy link',
    'About this file',
  ]);

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
