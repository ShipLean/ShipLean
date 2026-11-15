import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../touch/testIds';
import { MarkdownTouchHarness } from './ct/MarkdownTouchHarness';
import { componentTestIds as ids } from './testIds';

test('Markdown_WhenOnAPhone_ShouldUse15pxBodyText', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="plain" />);
  await expect(page.getByTestId(ids.markdown)).toHaveCSS('font-size', '15px');
});

test('Markdown_WhenATableIsWideOnAPhone_ShouldScrollInItsBoxNotThePage', async ({
  mount,
  page,
}) => {
  await mount(<MarkdownTouchHarness variant="wideTable" />);
  const box = page.getByTestId(touchTestIds.markdownScroll);
  await expect(box).toHaveCount(1);
  const size = await box.evaluate((el) => ({ client: el.clientWidth, scroll: el.scrollWidth }));
  expect(size.scroll).toBeGreaterThan(size.client);
  const page_ = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    inner: window.innerWidth,
  }));
  expect(page_.scroll).toBeLessThanOrEqual(page_.inner);
});

test('Markdown_WhenCodeIsWideOnAPhone_ShouldScrollInItsBox', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="wideCode" />);
  const box = page.getByTestId(touchTestIds.markdownScroll);
  await expect(box.locator('pre')).toHaveCount(1);
  const size = await box.evaluate((el) => ({ client: el.clientWidth, scroll: el.scrollWidth }));
  expect(size.scroll).toBeGreaterThan(size.client);
  const page_ = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    inner: window.innerWidth,
  }));
  expect(page_.scroll).toBeLessThanOrEqual(page_.inner);
});

test('Markdown_WhenScrolledToTheEnd_ShouldHideTheFade', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="wideTable" />);
  const box = page.getByTestId(touchTestIds.markdownScroll);
  await expect(box).toHaveAttribute('data-more', '');
  await expect(box).not.toHaveCSS('mask-image', 'none');
  await box.evaluate((el) => {
    el.scrollLeft = el.scrollWidth;
  });
  await expect(box).not.toHaveAttribute('data-more', '');
  await expect(box).toHaveCSS('mask-image', 'none');
});

test('Markdown_WhenAnImageIsWide_ShouldFitTheWidth', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="wideImage" />);
  const img = page.locator('figure[data-md-image=attached] img');
  await expect(img).toBeVisible();
  const box = await img.boundingBox();
  expect(box?.width).toBeLessThanOrEqual(390);
  // The picture is 3:1 and the image has a 1px border on each side.
  expect(box?.height).toBeCloseTo(((box?.width ?? 0) - 2) / 3 + 2, 0);
});

test('Markdown_WhenAnImageIsTappedOnAPhone_ShouldOpenTheViewerAtThatImage', async ({
  mount,
  page,
}) => {
  await mount(<MarkdownTouchHarness variant="twoImages" />);
  await expect(page.getByTestId(touchTestIds.imageViewer)).toHaveCount(0);
  await page.locator('figure[data-md-image=attached] img').nth(1).tap();
  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
  await expect(page.getByTestId(touchTestIds.imageViewerImage)).toHaveAttribute(
    'alt',
    'Second alt',
  );
});

test('Markdown_WhenViewerImagesGiven_ShouldOpenTheViewerOnThatSetAtTheTappedImage', async ({
  mount,
  page,
}) => {
  await mount(<MarkdownTouchHarness variant="viewerSet" />);
  await page.locator('figure[data-md-image=attached] img').tap();
  await expect(page.getByTestId(touchTestIds.imageViewerImage)).toHaveAttribute(
    'alt',
    'B from the set',
  );
});

test('Markdown_WhenViewerImagesLackTheTappedImage_ShouldOpenOnTheFirst', async ({
  mount,
  page,
}) => {
  await mount(<MarkdownTouchHarness variant="viewerSetMissing" />);
  await page.locator('figure[data-md-image=attached] img').tap();
  await expect(page.getByTestId(touchTestIds.imageViewerImage)).toHaveAttribute(
    'alt',
    'A from the set',
  );
});

test('ImageViewer_WhenCrossTapped_ShouldClose', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="twoImages" />);
  await page.locator('figure[data-md-image=attached] img').first().tap();
  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
  await page.getByTestId(touchTestIds.imageViewerClose).tap();
  await expect(page.getByTestId(touchTestIds.imageViewer)).toHaveCount(0);
});

test('ImageViewer_WhenEscPressed_ShouldClose', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="twoImages" />);
  await page.locator('figure[data-md-image=attached] img').first().tap();
  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(touchTestIds.imageViewer)).toHaveCount(0);
});

test('Markdown_WhenAMermaidBlockIsDrawn_ShouldNotWrapIt', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="mermaid" />);
  await expect(page.locator('[data-md-diagram=mermaid] svg')).toBeVisible();
  await expect(page.getByTestId(touchTestIds.markdownScroll)).toHaveCount(0);
});

test('Markdown_WhenShownOnAPhone_ShouldMatchTheScreenshot', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="shot" />);
  await expect(page.getByTestId(touchTestIds.markdownScroll).first()).toBeVisible();
  await expect(page).toHaveScreenshot('markdown-touch-light.png');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page).toHaveScreenshot('markdown-touch-dark.png');
});

test('ImageViewer_WhenOpenedOnAPhone_ShouldMatchTheScreenshot', async ({ mount, page }) => {
  await mount(<MarkdownTouchHarness variant="twoImages" />);
  await page.locator('figure[data-md-image=attached] img').first().tap();
  await expect(page.getByTestId(touchTestIds.imageViewerImage)).toBeVisible();
  await expect(page).toHaveScreenshot('image-viewer-stub-light.png');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page).toHaveScreenshot('image-viewer-stub-dark.png');
});
