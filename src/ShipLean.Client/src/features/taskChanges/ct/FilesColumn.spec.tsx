import { expect, test } from '@playwright/experimental-ct-react';
import { FilesColumn } from '../FilesColumn';
import { taskChangesTestIds as ids } from '../testIds';
import { fileKey } from '../types';
import { CHANGES } from './fixtures';

const [first, second] = CHANGES.parts[0].files;
const repo = CHANGES.parts[0].repo;
const column = (props: Partial<Parameters<typeof FilesColumn>[0]> = {}) => (
  <FilesColumn
    data={CHANGES}
    openFile={null}
    viewed={{}}
    onOpenFile={() => {}}
    onToggleViewed={() => {}}
    {...props}
  />
);

test('FilesColumn_WhenAFileIsSelected_ShouldCallOnOpenFileWithRepoAndPath', async ({
  mount,
  page,
}) => {
  const opened: string[] = [];
  await mount(column({ onOpenFile: (k) => opened.push(k) }));
  await page.getByTestId(ids.fileRow).nth(1).click();
  expect(opened).toEqual([fileKey(repo, second.path)]);
});

test('FilesColumn_WhenPatchIdsMatchTheMarks_ShouldTickOnlyThoseFiles', async ({ mount, page }) => {
  const viewed = {
    [`${repo}\u0000${first.path}`]: { patchId: first.patchId, headCommitId: null },
    // Marked with an older patch: not ticked.
    [`${repo}\u0000${second.path}`]: { patchId: 'stale', headCommitId: null },
  };
  await mount(column({ viewed }));
  const ticks = page.getByTestId(ids.fileViewed);
  await expect(ticks.nth(0)).toHaveText('✓');
  await expect(ticks.nth(1)).toHaveText('');
});

test('FilesColumn_WhenTwoRepositories_ShouldGroupByRepository', async ({ mount, page }) => {
  const other = { ...CHANGES.parts[0], repo: 'acme/api' };
  await mount(column({ data: { ...CHANGES, parts: [CHANGES.parts[0], other] } }));
  await expect(page.getByTestId(ids.files)).toContainText('acme/api');
  await expect(page.getByTestId(ids.fileRow)).toHaveCount(4);
});
