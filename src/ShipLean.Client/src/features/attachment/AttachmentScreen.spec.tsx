import { expect, test } from '@playwright/experimental-ct-react';
import type { AttachmentMarkdownDto } from '../../api/attachments';
import { markdownView, stubFeature } from '../feature/ct/stubFeature';
import { AttachmentHarness } from './ct/AttachmentHarness';
import { attachmentTestIds as ids } from './testIds';

const itemKey = markdownView.workItem.key;
const attachmentId = markdownView.attachment.id;
const mountProps = { itemKey, attachmentId };

// The part of Playwright's Page and Route the replace test's own stubs use.
type StubbedPage = Parameters<typeof stubFeature>[0];

test('AttachmentPage_ShouldRenderOutlineAndImages', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await expect(screen.getByTestId(ids.name)).toHaveText('vault-notes.md');
  await expect(screen.getByTestId(ids.workItem)).toContainText('SHIP-141 Saved card picker');
  await expect(screen.getByTestId(ids.meta)).toContainText('replaced twice');
  await expect(screen.getByTestId(ids.meta)).toContainText('Claude Code (claude.ai) (agent)');
  await expect(screen.getByTestId(ids.body).getByRole('heading', { name: 'Tokens' })).toBeVisible();
  await expect(
    screen.getByTestId(ids.body).locator('figure[data-md-image=attached]'),
  ).toBeVisible();
  await expect(screen.getByTestId(ids.outlineEntry)).toHaveText(['Vault notes', 'Tokens']);
  await expect(screen.getByTestId(ids.outlineEntry).last()).toHaveAttribute('href', '#tokens');
  await expect(screen.getByTestId(ids.imageRow)).toHaveCount(1);
  await expect(screen.getByTestId(ids.imageStatus)).toHaveText('attached');
  await expect(screen.getByTestId(ids.noHistory)).toBeVisible();
  await expect(screen.getByTestId(ids.sibling)).toHaveText([/vault-notes\.md/, /rollout-plan\.md/]);
  await expect(screen.getByTestId(ids.download)).toHaveAttribute(
    'href',
    markdownView.attachment.downloadUrl,
  );
});

test('AttachmentPage_WhenSourceToggled_ShouldShowMarkdown', async ({ mount, page }) => {
  await stubFeature(page);
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  await expect(screen.getByTestId(ids.body)).toBeVisible();

  await screen.getByTestId(ids.source).click();

  await expect(screen.getByTestId(ids.sourceText)).toHaveText(markdownView.markdown, {
    useInnerText: true,
  });
  await expect(screen.getByTestId(ids.body)).toHaveCount(0);
  await expect(screen.getByTestId(ids.source)).toHaveAttribute('aria-pressed', 'true');

  await screen.getByTestId(ids.rendered).click();
  await expect(screen.getByTestId(ids.body)).toBeVisible();
});

test('AttachmentPage_WhenNotFound_ShouldShowNotFoundState', async ({ mount, page }) => {
  const stub = await stubFeature(page);
  stub.failNext('markdownView', 404, { error: 'Attachment not found.', code: 'not_found' });
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await expect(screen.getByTestId(ids.notFound)).toContainText('Attachment not found.');
  await expect(screen.getByTestId(ids.back)).toBeVisible();
  await expect(screen.getByTestId(ids.name)).toHaveCount(0);
});

test('AttachmentPage_WhenTooLarge_ShouldOfferDownload', async ({ mount, page }) => {
  const stub = await stubFeature(page);
  stub.failNext('markdownView', 400, {
    error: 'This file is too large to show.',
    code: 'markdown_too_large',
  });
  const screen = await mount(<AttachmentHarness {...mountProps} />);

  await expect(screen.getByTestId(ids.loadFailed)).toContainText('too large');
  await expect(screen.getByTestId(ids.loadFailedDownload)).toHaveAttribute(
    'href',
    `/api/attachments/${attachmentId}/content`,
  );
});

test('AttachmentPage_WhenReplaced_ShouldRefetch', async ({ mount, page }) => {
  await stubFeature(page);
  // The second read answers the replaced file; routes added later win over stubFeature's.
  const replaced: AttachmentMarkdownDto = {
    ...markdownView,
    attachment: { ...markdownView.attachment, replacedCount: 3, sizeBytes: 4096 },
    html: '<h1 id="new">Replaced body</h1>',
    outline: [{ level: 1, text: 'Replaced body', anchor: 'new' }],
  };
  let reads = 0;
  let uploads = 0;
  const stubbed: StubbedPage = page;
  await stubbed.route('**/api/attachments/*/markdown', (route) => {
    reads += 1;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(uploads === 0 ? markdownView : replaced),
    });
  });
  await stubbed.route('**/api/work-items/*/attachments', (route) => {
    uploads += 1;
    return route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify(replaced.attachment),
    });
  });
  const screen = await mount(<AttachmentHarness {...mountProps} />);
  await expect(screen.getByTestId(ids.meta)).toContainText('replaced twice');

  await screen.getByTestId(ids.replaceInput).setInputFiles({
    name: 'vault-notes.md',
    mimeType: 'text/markdown',
    buffer: Buffer.from('# Replaced body'),
  });

  await expect(screen.getByTestId(ids.meta)).toContainText('replaced 3 times');
  await expect(screen.getByTestId(ids.body)).toContainText('Replaced body');
  expect(uploads).toBe(1);
  expect(reads).toBe(2);
});
