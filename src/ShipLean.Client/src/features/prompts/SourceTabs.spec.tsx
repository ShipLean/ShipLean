import { expect, test } from '@playwright/experimental-ct-react';
import { openByAdminFlag } from './ct/openPrompt';
import { adminPage } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

test('SourceTab_WhenSectionErrors_ShouldWarnThatOnlyUntaggedTextIsServed', async ({
  mount,
  page,
}) => {
  await openByAdminFlag(mount, page, 'guide');

  await expect(page.getByTestId(ids.sectionErrorFlag)).toContainText(
    'every agent gets only the untagged text',
  );
});

test('SourceTab_WhenCustom_ShouldHighlightPlaceholdersAndAgentTags', async ({ mount, page }) => {
  const custom = adminPage.stages.flatMap((s) => s.prompts).find((p) => p.key === 'brief')?.custom;
  const text =
    '# Brief\nFor {{key}} here\n<claude-code>\nonly this\n</claude-code>\nmid <cursor> tag\n';
  const edited = {
    ...adminPage,
    stages: adminPage.stages.map((s) => ({
      ...s,
      prompts: s.prompts.map((p) =>
        p.key === 'brief' && custom ? { ...p, custom: { ...custom, text } } : p,
      ),
    })),
  };
  await openByAdminFlag(mount, page, 'brief', true, edited);

  await expect(page.getByTestId(ids.sourceHeading)).toHaveText('# Brief');
  await expect(page.getByTestId(ids.sourcePlaceholder)).toHaveText('{{key}}');
  await expect(page.getByTestId(ids.sourceAgentTag)).toHaveCount(2);
  await expect(page.getByTestId(ids.sectionErrorFlag)).toHaveCount(0);
});

test('SourceTab_WhenCustom_ShouldColourPlaceholdersAndAgentTagsApartFromTheText', async ({
  mount,
  page,
}) => {
  const custom = adminPage.stages.flatMap((s) => s.prompts).find((p) => p.key === 'brief')?.custom;
  const text = 'For {{key}} here\n<claude-code>\nonly this\n</claude-code>\n';
  const edited = {
    ...adminPage,
    stages: adminPage.stages.map((s) => ({
      ...s,
      prompts: s.prompts.map((p) =>
        p.key === 'brief' && custom ? { ...p, custom: { ...custom, text } } : p,
      ),
    })),
  };
  await openByAdminFlag(mount, page, 'brief', true, edited);

  const source = page.getByTestId(ids.panelSource).getByTestId(ids.sourceText);
  await expect(source).toBeVisible();
  const textColour = await source.evaluate((el) => getComputedStyle(el).color);
  for (const span of [
    page.getByTestId(ids.sourcePlaceholder),
    page.getByTestId(ids.sourceAgentTag).first(),
  ]) {
    const style = await span.evaluate((el) => {
      const computed = getComputedStyle(el);
      return { color: computed.color, background: computed.backgroundColor };
    });
    expect(style.color).not.toBe(textColour);
    expect(style.background).not.toBe('rgba(0, 0, 0, 0)');
  }
  const placeholder = await page
    .getByTestId(ids.sourcePlaceholder)
    .evaluate((el) => getComputedStyle(el).color);
  const agentTag = await page
    .getByTestId(ids.sourceAgentTag)
    .first()
    .evaluate((el) => getComputedStyle(el).color);
  expect(placeholder).not.toBe(agentTag);
});

test('RenderedTab_ShouldShowTheServerRenderedHtml', async ({ mount, page }) => {
  await page.route('**/api/markdown/render', (route) =>
    route.fulfill({
      json: { markdown: '', html: '<h2>Server rendered</h2>', images: [] },
    }),
  );
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabRendered).click();

  await expect(page.getByTestId(ids.renderedBody).getByRole('heading')).toHaveText(
    'Server rendered',
  );
});

test('SourceTab_ShouldRenderWithoutLineNumbers', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'work');

  const source = page.getByTestId(ids.panelSource).getByTestId(ids.sourceText);
  await expect(source).toBeVisible();
  const first = adminPage.stages
    .flatMap((s) => s.prompts)
    .find((p) => p.key === 'work')!
    .builtInText.split('\n')[0];
  expect((await source.innerText()).split('\n')[0]).toBe(first);
  await expect(source.getByTestId(ids.sourceLineNumber)).toHaveCount(0);
});
