import { expect, test } from '@playwright/experimental-ct-react';
import type { TaskChangesDto } from '@shiplean/core/api/taskChanges';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { ChangesScreen } from '../ChangesScreen';
import { taskChangesTestIds as ids } from '../testIds';
import { CHANGES, SECTION, TASK } from './fixtures';
import { stubChanges } from './stubChanges';

test.use({ viewport: { width: 1280, height: 800 } });

const part = CHANGES.parts[0];
// A file that deletes line 2 and adds one, so the new side has a numbered line to click.
const swap = {
  ...part.files[0],
  path: 'src/swap.ts',
  patch: '@@ -1,3 +1,3 @@\n keep\n-old line\n+new line\n tail\n',
  patchId: 'swap-patch',
};
const bare: TaskChangesDto = {
  ...CHANGES,
  parts: [{ ...part, files: [swap] }],
  comments: [],
  openComments: 0,
};

test('ChangesScreen_WhenPlusClickedOnDiffLine_ShouldPostAnchorAndShowMarker', async ({
  mount,
  page,
}) => {
  await stubChanges(page, TASK, bare, SECTION);
  const bodies: Record<string, unknown>[] = [];
  await page.route(`**/api/work-items/${TASK.id}/code-comments`, async (route) => {
    const body = JSON.parse(route.request().postData() ?? '{}') as Record<string, unknown>;
    bodies.push(body);
    const comment = {
      ...CHANGES.comments[0],
      repo: body.repo,
      sha: body.sha,
      path: body.path,
      side: body.side,
      line: body.line,
      lineText: body.lineText,
    };
    // From now on the page read carries the new comment, as the refetch after the POST sees it.
    await page.route(`**/api/work-items/${TASK.id}/changes?*`, (r) =>
      r.fulfill({ json: { ...bare, comments: [comment], openComments: 1 } }),
    );
    await route.fulfill({ json: comment });
  });
  await mount(<ChangesScreen project={PROJECT} itemKey={TASK.key} />, {
    hooksConfig: { route: `/p/SHIP/items/${TASK.key}/changes` },
  });

  await expect(page.getByTestId(ids.diffFile)).toHaveCount(1);
  await page
    .locator('[data-gutter] [data-line-type="change-addition"][data-column-number="2"]')
    .first()
    .hover();
  await page.locator('[data-utility-button]').click();
  await expect(page.getByTestId(ids.composer)).toBeVisible();
  await page.getByTestId(ids.composerBody).fill('Why not keep the old line?');
  await page.getByTestId(ids.composerPost).click();

  await expect(page.getByTestId(ids.marker)).toBeVisible();
  await expect(page.getByTestId(ids.composer)).toHaveCount(0);
  expect(bodies).toEqual([
    {
      repo: part.repo,
      sha: part.headSha,
      path: 'src/swap.ts',
      side: 'new',
      line: 2,
      startLine: null,
      lineText: 'new line',
      body: 'Why not keep the old line?',
    },
  ]);
});
