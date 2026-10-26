import { expect, test, type MountResult } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { RenderedMarkdownDto } from '../../../../api/markdown';
import { feature, stubFeature } from '../../../feature/ct/stubFeature';
import type { WorkItemDto } from '../../../../api/workItems';
import { bug, priya } from '../../ct/fixtures';
import { stubApi } from '../../ct/stubApi';
import { stubComments } from '../../ct/stubDetails';
import { attachment, stubSide } from '../../ct/stubSide';
import { WorkItemFormHarness } from '../../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../../testIds';

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

const external = 'https://example.com/diagram.png';

// The feature form (or `item`'s form) with its calls stubbed. The render stub lists every `![](https://...)` in the markdown as external.
async function mountForm(
  page: StubbedPage,
  mount: (c: React.JSX.Element, o: { hooksConfig: { route: string } }) => Promise<MountResult>,
  existing = [attachment('a1', 'pasted-1.png', priya)],
  item: WorkItemDto = feature,
) {
  const api = await stubApi(page, item);
  const side = await stubSide(page, priya, existing, []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  await page.route('**/api/markdown/render', (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    const images: RenderedMarkdownDto['images'] = markdown.includes(external)
      ? [{ src: external, status: 'external', attachmentId: null }]
      : [];
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ markdown, html: `<p>rendered:${markdown}</p>`, images }),
    });
  });
  const screen = await mount(<WorkItemFormHarness itemKey={item.key} />, {
    hooksConfig: { route: `/p/SHIP/items/${item.key}` },
  });
  await screen.getByTestId(ids.descriptionEdit).click();
  return { api, side, screen };
}

// The clipboard as Playwright can give it: a DataTransfer holding an unnamed PNG, pasted on the textarea.
const pasteImage = (textarea: Locator) =>
  textarea.evaluate((element: HTMLElement) => {
    const data = new DataTransfer();
    data.items.add(new File([new Uint8Array([137, 80, 78, 71])], '', { type: 'image/png' }));
    element.dispatchEvent(
      new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
    );
  });

test('Description_WhenImagePasted_ShouldUploadAndInsertReference', async ({ mount, page }) => {
  const { side, screen } = await mountForm(page, mount);
  const input = screen.getByTestId(ids.descriptionInput);
  await input.fill('Before after');
  await input.evaluate((element: HTMLTextAreaElement) => element.setSelectionRange(7, 7));

  await pasteImage(input);

  await expect(input).toHaveValue('Before ![](pasted-2.png)after');
  expect(side.uploaded).toEqual(['pasted-2.png']);
  await expect(screen.getByTestId(ids.descriptionPasteError)).toHaveCount(0);
});

test('ReproSteps_WhenImagePasted_ShouldUploadAndInsertReference', async ({ mount, page }) => {
  const { side, screen } = await mountForm(page, mount, undefined, bug);
  await screen.getByTestId(ids.reproEdit).click();
  const input = screen.getByTestId(ids.reproInput);
  await input.fill('1. Save');

  await pasteImage(input);

  await expect(input).toHaveValue('1. Save![](pasted-2.png)');
  expect(side.uploaded).toEqual(['pasted-2.png']);
  await expect(screen.getByTestId(ids.reproPasteError)).toHaveCount(0);
});

test('Description_WhenPasteUploadFails_ShouldShowErrorAndInsertNothing', async ({
  mount,
  page,
}) => {
  const { side, screen } = await mountForm(page, mount);
  const input = screen.getByTestId(ids.descriptionInput);
  await input.fill('Text');
  side.refuseNext('That file is too large to attach.');

  await pasteImage(input);

  await expect(screen.getByTestId(ids.descriptionPasteError)).toHaveText(
    'That file is too large to attach.',
  );
  await expect(input).toHaveValue('Text');
  expect(side.uploaded).toEqual([]);
});

test('Description_WhenExternalImage_ShouldWarnBeforeSave', async ({ mount, page }) => {
  const { api, screen } = await mountForm(page, mount);
  await screen.getByTestId(ids.descriptionInput).fill(`See ![](${external})`);

  await expect(screen.getByTestId(ids.descriptionExternalNote)).toContainText(external);
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveConfirmMessage)).toContainText(external);
  expect(api.patches).toHaveLength(0);
  await screen.getByTestId(ids.saveConfirmOk).click();
  await expect.poll(() => api.patches.length).toBe(1);
});

test('Description_WhenNoExternalImage_ShouldSaveWithoutPrompt', async ({ mount, page }) => {
  const { api, screen } = await mountForm(page, mount);
  const rendered = page.waitForResponse(
    (response) =>
      response.url().includes('/api/markdown/render') &&
      (response.request().postData() ?? '').includes('Plain words'),
  );
  await screen.getByTestId(ids.descriptionInput).fill('Plain words');
  await rendered;

  await screen.getByTestId(ids.save).click();

  await expect.poll(() => api.patches.length).toBe(1);
  await expect(screen.getByTestId(ids.saveConfirm)).toHaveCount(0);
  await expect(screen.getByTestId(ids.descriptionExternalNote)).toHaveCount(0);
});

test('Description_WhenSavedBeforeRenderReturns_ShouldStillAsk', async ({ mount, page }) => {
  const { api, screen } = await mountForm(page, mount);
  let release = () => {};
  let requested = () => {};
  const released = new Promise<void>((resolve) => (release = resolve));
  const asked = new Promise<void>((resolve) => (requested = resolve));
  await page.route('**/api/markdown/render', async (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    requested();
    await released;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ markdown, html: '', images: [] }),
    });
  });
  await screen.getByTestId(ids.descriptionInput).fill('Plain words');
  await asked;

  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveConfirmMessage)).toContainText(
    'Images in the description are still being checked.',
  );
  expect(api.patches).toHaveLength(0);
  release();
});

test('Description_WhenRenderFails_ShouldStillAsk', async ({ mount, page }) => {
  const { api, screen } = await mountForm(page, mount);
  const failed = page.waitForResponse(
    (response) => response.url().includes('/api/markdown/render') && response.status() === 500,
  );
  await page.route('**/api/markdown/render', (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Render failed.', code: 'validation' }),
    }),
  );
  await screen.getByTestId(ids.descriptionInput).fill('Plain words');
  await failed;

  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveConfirmMessage)).toContainText(
    "Images in the description couldn't be checked.",
  );
  expect(api.patches).toHaveLength(0);
});

test('Description_WhenTyping_ShouldRenderOnceAfterPause', async ({ mount, page }) => {
  const { screen } = await mountForm(page, mount);
  const bodies: string[] = [];
  await page.route('**/api/markdown/render', (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    bodies.push(markdown);
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ markdown, html: `<p>${markdown}</p>`, images: [] }),
    });
  });
  const rendered = page.waitForResponse(
    (response) =>
      response.url().includes('/api/markdown/render') &&
      (response.request().postData() ?? '').includes('typed fast'),
  );
  const input = screen.getByTestId(ids.descriptionInput);
  await input.fill('');

  await input.pressSequentially('typed fast');
  await rendered;

  expect(bodies).toEqual(['typed fast']);
});

test('Description_WhenBoldPressed_ShouldWrapTheWord', async ({ mount, page }) => {
  const { screen } = await mountForm(page, mount);
  const input = screen.getByTestId(ids.descriptionInput);
  await input.fill('card');

  await screen.getByRole('button', { name: 'Add bold text (ctrl + b)' }).click();

  await expect(input).toHaveValue('**card**');
});
