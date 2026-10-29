import { expect, test } from '@playwright/experimental-ct-react';
import { Markdown } from './Markdown';
import { componentTestIds as ids } from './testIds';

test('Markdown_WhenHtmlGiven_ShouldRenderIt', async ({ mount, page }) => {
  await mount(<Markdown html="<h2>Heading</h2><p>Some <strong>bold</strong> text</p>" />);
  const markdown = page.getByTestId(ids.markdown);
  await expect(markdown.locator('h2')).toHaveText('Heading');
  await expect(markdown.locator('strong')).toHaveText('bold');
});

test('Markdown_WhenImagePlaceholders_ShouldDrawThemDashed', async ({ mount, page }) => {
  await mount(
    <Markdown html='<p><span data-md-image="external" data-src="https://example.com/a.png">External image not shown</span> <span data-md-image="missing">gone.png</span></p>' />,
  );
  const markdown = page.getByTestId(ids.markdown);
  await expect(markdown.locator('[data-md-image="external"]')).toHaveCSS('border-style', 'dashed');
  await expect(markdown.locator('[data-md-image="missing"]')).toHaveCSS('border-style', 'dashed');
});

test('Markdown_WhenTableIsWiderThanItsContainer_ShouldScrollInsideIt', async ({ mount, page }) => {
  const cells = Array.from(
    { length: 12 },
    (_, i) => `<td><code>column_${i}_with_a_long_name</code></td>`,
  );
  await mount(
    <div style={{ width: 300 }}>
      <Markdown html={`<table><tbody><tr>${cells.join('')}</tr></tbody></table>`} />
    </div>,
  );
  const markdown = page.getByTestId(ids.markdown);
  const table = markdown.locator('table');

  expect((await markdown.boundingBox())?.width).toBe(300);
  const size = await table.evaluate((el) => ({ client: el.clientWidth, scroll: el.scrollWidth }));
  expect(size.client).toBeLessThanOrEqual(300);
  expect(size.scroll).toBeGreaterThan(300);
});

test('Markdown_WhenMermaidBlock_ShouldDrawItAsADiagram', async ({ mount, page }) => {
  await mount(
    <Markdown
      html={
        '<pre><code class="language-mermaid">sequenceDiagram\n    participant U as Person\n    U-&gt;&gt;U: Hello\n</code></pre>'
      }
    />,
  );
  const markdown = page.getByTestId(ids.markdown);
  await expect(markdown.locator('[data-md-diagram=mermaid] svg')).toBeVisible();
  await expect(markdown.locator('[data-md-diagram=mermaid]')).toContainText('Person');
  await expect(markdown.locator('code.language-mermaid')).toHaveCount(0);
});

test('Markdown_WhenMermaidBlockIsInvalid_ShouldKeepTheCodeAndSaySo', async ({ mount, page }) => {
  await mount(
    <Markdown html={'<pre><code class="language-mermaid">not a diagram at all</code></pre>'} />,
  );
  const markdown = page.getByTestId(ids.markdown);
  await expect(markdown.locator('[data-md-diagram-error]')).toContainText('could not be drawn');
  await expect(markdown.locator('code.language-mermaid')).toHaveText('not a diagram at all');
});
