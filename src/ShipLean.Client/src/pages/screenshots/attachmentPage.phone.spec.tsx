import { expect } from '@playwright/experimental-ct-react';
import { attachmentTestIds as ids } from '../../features/attachment/testIds';
import { touchTestIds } from '../../touch/testIds';
import { checkoutV2, designView, stubFeaturePage } from './fixtures/feature';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// The Markdown attachment page on a phone (SHIP-715, L715a): the header, the scrolled page with the Contents pill, the
// Contents, About and file sheets, the ⋯ sheet and the too-large state. The Source view is L715c's, with the body.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/attachmentPage.phone.spec.tsx.

const sections = ['Flow', 'Tokens', 'Open questions', 'Rollout'];
const filler = (name: string) =>
  `<p>${name} keeps card numbers out of our database: the vault returns a token, and everything after the checkout form only ever sees the token.</p>`.repeat(
    5,
  );
const longView = {
  ...designView,
  html: [
    '<h1 id="design">Design</h1>',
    filler('Design'),
    ...sections.map(
      (name) => `<h2 id="${name.toLowerCase().replace(' ', '-')}">${name}</h2>${filler(name)}`,
    ),
  ].join(''),
  outline: [
    { level: 1, text: 'Design', anchor: 'design' },
    ...sections.map((name) => ({
      level: 2,
      text: name,
      anchor: name.toLowerCase().replace(' ', '-'),
    })),
  ],
};
const route = `/p/SHIP/items/SHIP-100/attachments/${designView.attachment.id}`;

const arrange = async (page: Page) => {
  await stubFeaturePage(page, checkoutV2, longView);
};
const ready = async (page: Page) => {
  await expect(page.getByTestId(ids.name)).toHaveText('design-v0.2.md');
  await expect(page.getByTestId(ids.body)).toBeVisible();
  await expect(page.getByTestId(ids.contentsPill)).toContainText('Design');
};
const sheetOpened = (open: (page: Page) => Promise<void>, title: string) => async (page: Page) => {
  await open(page);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText(title);
};

phonePageShots([
  { name: 'attachment-rendered', route, shell: true, arrange, ready },
  {
    name: 'attachment-scrolled',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page
        .getByTestId(ids.body)
        .locator('#open-questions')
        .evaluate((heading) => heading.scrollIntoView({ block: 'start' }));
      await expect(page.getByTestId(ids.contentsPill)).toContainText('Open questions');
    },
  },
  {
    name: 'attachment-contents',
    route,
    shell: true,
    arrange,
    ready,
    act: sheetOpened((page) => page.getByTestId(ids.contentsPill).tap(), 'On this page'),
  },
  {
    name: 'attachment-about',
    route,
    shell: true,
    arrange,
    ready,
    act: sheetOpened((page) => page.getByTestId(ids.facts).tap(), 'About this file'),
  },
  {
    name: 'attachment-files',
    route,
    shell: true,
    arrange,
    ready,
    act: sheetOpened((page) => page.getByTestId(ids.nameButton).tap(), 'Markdown on SHIP-100'),
  },
  {
    name: 'attachment-menu',
    route,
    shell: true,
    arrange,
    ready,
    act: sheetOpened(
      (page) => page.getByTestId(touchTestIds.moreButton).tap(),
      'Attachment actions',
    ),
  },
  {
    name: 'attachment-toolarge',
    route,
    shell: true,
    arrange: async (page) => {
      await arrange(page);
      await page.route('**/api/attachments/*/markdown', (route) =>
        route.fulfill({
          status: 400,
          json: {
            error: 'This file is too large to show (over 256 KB). Download it instead.',
            code: 'markdown_too_large',
          },
        }),
      );
    },
    ready: async (page) => {
      await expect(page.getByTestId(ids.loadFailed)).toContainText('too large');
      await expect(page.getByTestId(ids.loadFailedDownload)).toBeVisible();
    },
  },
]);
