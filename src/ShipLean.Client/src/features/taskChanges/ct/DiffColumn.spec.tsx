import { expect, test } from '@playwright/experimental-ct-react';
import type { TaskChangesDto } from '@shiplean/core/api/taskChanges';
import { DiffColumn } from '../DiffColumn';
import type { DiffColumnProps } from '../DiffColumn';
import { taskChangesTestIds as ids } from '../testIds';
import { fileKey } from '../types';
import { CHANGES } from './fixtures';

const part = CHANGES.parts[0];
const [first, second] = part.files;

const column = (props: Partial<DiffColumnProps> = {}) => (
  <DiffColumn
    data={CHANGES}
    layout="unified"
    openFile={null}
    markers={[]}
    viewed={{}}
    onAddLine={() => {}}
    onToggleViewed={() => {}}
    composerTarget={null}
    composer={null}
    {...props}
  />
);

// A file that deletes line 2 and adds one, so both sides have a numbered line.
const swap = {
  ...first,
  path: 'src/swap.ts',
  patch: '@@ -1,3 +1,3 @@\n keep\n-old line\n+new line\n tail\n',
  patchId: 'swap-patch',
};
const withFiles = (files: TaskChangesDto['parts'][number]['files'], sha: string | null = null) =>
  ({ ...CHANGES, parts: [{ ...part, sha, files }] }) satisfies TaskChangesDto;

type Page = { locator(selector: string): Locator };
type Locator = { first(): Locator; hover(): Promise<void> };
const line = (page: Page, type: string, n: number) =>
  page.locator(`[data-gutter] [data-line-type="${type}"][data-column-number="${n}"]`).first();

test('DiffColumn_WhenAPartHasTwoFiles_ShouldRenderEachFile', async ({ mount, page }) => {
  await mount(column());
  const files = page.getByTestId(ids.diffFile);
  await expect(files).toHaveCount(2);
  await expect(files.nth(0)).toContainText(first.path);
  await expect(files.nth(1)).toContainText(second.path);
  await expect(page.getByText('import { SavedCards }')).toBeVisible();
});

test('DiffColumn_WhenLayoutIsSplit_ShouldShowOldAndNewSideBySide', async ({ mount, page }) => {
  await mount(column({ data: withFiles([swap]), layout: 'split' }));
  await expect(page.locator('[data-deletions]')).toBeVisible();
  await expect(page.locator('[data-additions]')).toBeVisible();
  await expect(page.locator('[data-unified]')).toHaveCount(0);
  await expect(page.locator('[data-deletions]').getByText('old line')).toBeVisible();
  await expect(page.locator('[data-additions]').getByText('new line')).toBeVisible();
});

test('DiffColumn_WhenPlusOnAnOldLineIsClicked_ShouldCallOnAddLineWithRepoSideLineAndText', async ({
  mount,
  page,
}) => {
  const targets: unknown[] = [];
  await mount(column({ data: withFiles([swap]), onAddLine: (t) => targets.push(t) }));
  await line(page, 'change-deletion', 2).hover();
  await page.locator('[data-utility-button]').click();
  expect(targets).toEqual([
    {
      repo: part.repo,
      sha: part.headSha,
      path: 'src/swap.ts',
      side: 'old',
      line: 2,
      startLine: null,
      lineText: 'old line',
    },
  ]);
});

test('DiffColumn_WhenPlusOnANewLineIsClicked_ShouldAnchorToTheNewSide', async ({ mount, page }) => {
  const targets: { side: string; line: number; lineText: string }[] = [];
  await mount(column({ data: withFiles([swap]), onAddLine: (t) => targets.push(t) }));
  await line(page, 'change-addition', 2).hover();
  await page.locator('[data-utility-button]').click();
  expect(targets).toMatchObject([{ side: 'new', line: 2, lineText: 'new line' }]);
});

test('DiffColumn_WhenAPartIsOneCommit_ShouldAnchorPlusToThatCommitsSha', async ({
  mount,
  page,
}) => {
  const sha = CHANGES.commits[0].sha;
  const targets: { sha: string }[] = [];
  await mount(column({ data: withFiles([swap], sha), onAddLine: (t) => targets.push(t) }));
  await line(page, 'change-addition', 2).hover();
  await page.locator('[data-utility-button]').click();
  expect(targets).toMatchObject([{ sha }]);
});

test('DiffColumn_WhenAComposerTargetIsSet_ShouldRenderTheComposerUnderThatLine', async ({
  mount,
  page,
}) => {
  const target = {
    repo: part.repo,
    sha: part.headSha,
    path: 'src/swap.ts',
    side: 'new' as const,
    line: 2,
    startLine: null,
    lineText: 'new line',
  };
  await mount(
    column({
      data: withFiles([swap]),
      composerTarget: target,
      composer: <div data-testid={ids.composer}>composing</div>,
    }),
  );
  const composer = page.getByTestId(ids.composer);
  await expect(composer).toBeVisible();
  const lineBox = await page.getByText('new line').last().boundingBox();
  const composerBox = await composer.boundingBox();
  expect(composerBox!.y).toBeGreaterThan(lineBox!.y);
  // Under line 2, above the context line that follows it.
  const tailBox = await page.getByText('tail').first().boundingBox();
  expect(composerBox!.y).toBeLessThan(tailBox!.y);
});

test('DiffColumn_WhenMarkersAreGiven_ShouldDrawThemOnTheirLines', async ({ mount, page }) => {
  const marker = {
    key: 'k',
    repo: part.repo,
    sha: part.headSha,
    path: 'src/swap.ts',
    side: 'new' as const,
    line: 2,
    count: 2,
    open: 1,
    outdated: false,
  };
  await mount(
    column({
      data: withFiles([swap]),
      markers: [marker, { ...marker, key: 'o', line: 1, outdated: true, count: 1 }],
    }),
  );
  await expect(page.getByTestId(ids.marker)).toContainText('2 comments');
  await expect(page.getByTestId(ids.markerOutdated)).toContainText('outdated');
});

test('DiffColumn_WhenViewedIsToggledAndMarked_ShouldCallBackAndCollapseTheFile', async ({
  mount,
  page,
}) => {
  const calls: unknown[][] = [];
  const component = await mount(column({ onToggleViewed: (...a) => calls.push(a) }));
  const file = page.getByTestId(ids.diffFile).first();
  await expect(file.getByText('import { SavedCards }')).toBeVisible();
  await file.getByTestId(ids.fileViewed).click();
  expect(calls).toEqual([[part.repo, first.path, first.patchId, true]]);
  await component.update(
    column({
      viewed: {
        [`${part.repo}\u0000${first.path}`]: { patchId: first.patchId, headCommitId: null },
      },
    }),
  );
  await expect(file.getByText('import { SavedCards }')).toHaveCount(0);
  await expect(file.getByTestId(ids.fileViewed)).toBeChecked();
});

test('DiffColumn_WhenACommitHasNoAccess_ShouldShowOpenOnHostLinks', async ({ mount, page }) => {
  const [a, b] = CHANGES.commits;
  const data = {
    ...CHANGES,
    parts: [],
    commits: [
      { ...a, access: 'unreadable' as const },
      { ...b, access: 'missing' as const },
    ],
  } satisfies TaskChangesDto;
  await mount(column({ data }));
  await expect(page.getByTestId(ids.diffNoAccess)).toHaveCount(2);
  const links = page.getByTestId(ids.diffOpenOnHost);
  await expect(links.nth(0)).toHaveAttribute('href', a.commitUrl);
  await expect(links.nth(1)).toHaveAttribute('href', b.commitUrl);
  await expect(page.getByTestId(ids.diffFile)).toHaveCount(0);
});

test('DiffColumn_WhenOpenFileIsSet_ShouldScrollToThatFile', async ({ mount, page }) => {
  const tall = { ...first, patch: `@@ -0,0 +1,80 @@\n${'+x\n'.repeat(80)}` };
  await mount(
    <div style={{ height: 200, overflow: 'auto' }}>
      {column({ data: withFiles([tall, second]), openFile: fileKey(part.repo, second.path) })}
    </div>,
  );
  const target = page.locator(`[data-file-key="${fileKey(part.repo, second.path)}"]`);
  // The first file renders 80 lines after mount; the scroll must still end on the second file.
  await expect(page.locator('[data-gutter] [data-column-number="80"]')).toBeVisible();
  await expect(target).toBeInViewport();
});

test('DiffColumn_WhenAFileHasAHostLink_ShouldPointAtTheBlobAtTheHeadSha', async ({
  mount,
  page,
}) => {
  await mount(column());
  const link = page.getByTestId(ids.diffFile).first().getByTestId(ids.diffFileHostLink);
  await expect(link).toHaveAttribute(
    'href',
    `https://github.com/acme/storefront/blob/${part.headSha}/${first.path}`,
  );
  await expect(link).toHaveText('GitHub ↗');
});

test('DiffColumn_WhenTheChevronIsPressed_ShouldCollapseAndExpandWithoutMarkingViewed', async ({
  mount,
  page,
}) => {
  const calls: unknown[] = [];
  await mount(column({ onToggleViewed: (...a) => calls.push(a) }));
  const file = page.getByTestId(ids.diffFile).first();
  await expect(file.getByText('import { SavedCards }')).toBeVisible();
  await file.getByTestId(ids.diffFileCollapse).click();
  await expect(file.getByText('import { SavedCards }')).toHaveCount(0);
  await expect(file.getByTestId(ids.diffFileCollapse)).toHaveAttribute('aria-expanded', 'false');
  await file.getByTestId(ids.diffFileCollapse).click();
  await expect(file.getByText('import { SavedCards }')).toBeVisible();
  expect(calls).toEqual([]);
});
