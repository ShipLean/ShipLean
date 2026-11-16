import { expect, test } from '@playwright/experimental-ct-react';
import { CodeCommentComposer } from '../CodeCommentComposer';
import { taskChangesTestIds as ids } from '../testIds';

const target = {
  repo: 'acme/storefront',
  sha: 'a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8',
  path: 'src/checkout/SavedCards.tsx',
  side: 'new' as const,
  line: 2,
  startLine: null,
  lineText: '  return null;',
};

test('CodeCommentComposer_WhenCtrlEnter_ShouldCallOnPostWithBody', async ({ mount, page }) => {
  const posted: string[] = [];
  await mount(
    <CodeCommentComposer
      target={target}
      onPost={async (b) => {
        posted.push(b);
      }}
      onCancel={() => {}}
    />,
  );
  await page.getByTestId(ids.composerBody).fill(' Why not 502? ');
  await page.getByTestId(ids.composerBody).press('Control+Enter');
  expect(posted).toEqual(['Why not 502?']);
});

test('CodeCommentComposer_WhenEmpty_ShouldNotPost', async ({ mount, page }) => {
  const posted: string[] = [];
  await mount(
    <CodeCommentComposer
      target={target}
      onPost={async (b) => {
        posted.push(b);
      }}
      onCancel={() => {}}
    />,
  );
  await page.getByTestId(ids.composerBody).press('Control+Enter');
  await expect(page.getByTestId(ids.composerPost)).toBeDisabled();
  expect(posted).toEqual([]);
});

test('CodeCommentComposer_WhenEscapeOrCancel_ShouldCallOnCancel', async ({ mount, page }) => {
  let cancels = 0;
  await mount(
    <CodeCommentComposer target={target} onPost={async () => {}} onCancel={() => (cancels += 1)} />,
  );
  await page.getByTestId(ids.composerBody).press('Escape');
  await page.getByTestId(ids.composerCancel).click();
  expect(cancels).toBe(2);
});
