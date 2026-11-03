import { expect, test } from '@playwright/experimental-ct-react';
import { AuthorChip } from './AuthorChip';
import { agentPoint } from './ct/fixtures';
import { retroSharedTestIds as ids } from './testIds';

test('AuthorChip_WhenTheAgentNameIsLongerThanItsContainer_ShouldStayInsideIt', async ({
  mount,
  page,
}) => {
  const author = { ...agentPoint.author, agentName: 'Claude Code · a very long conductor name' };
  await mount(
    <div data-testid="chip-box" style={{ width: 150 }}>
      <AuthorChip author={author} />
    </div>,
  );
  const box = await page.getByTestId('chip-box').boundingBox();
  const chip = await page.getByTestId(ids.retroAuthorChip).boundingBox();
  expect(chip!.x + chip!.width).toBeLessThanOrEqual(box!.x + box!.width + 0.5);
});
