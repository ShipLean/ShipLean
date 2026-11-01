import { expect, test } from '@playwright/experimental-ct-react';
import { sprintNames, strips } from './fixtures';
import { MentionStrip } from './MentionStrip';
import { retroSharedTestIds as ids } from './testIds';

const withFix = strips.find((s) => s.fixAt !== null)!;
const withoutFix = { ...withFix, fixAt: null };

test('MentionStrip_WhenRendered_ShouldHaveOneBarPerCount', async ({ mount, page }) => {
  await mount(<MentionStrip strip={withFix} sprintNames={sprintNames} />);
  await expect(page.getByTestId(ids.retroMentionStrip).locator('rect')).toHaveCount(
    withFix.counts.length,
  );
});

test('MentionStrip_WhenFixAtGiven_ShouldDrawTheLineAfterThatBar', async ({ mount, page }) => {
  await mount(<MentionStrip strip={withFix} sprintNames={sprintNames} width={120} />);
  const line = page.getByTestId(ids.retroMentionStrip).locator('line');
  await expect(line).toHaveAttribute('data-fix-at', String(withFix.fixAt));
  const barWidth = 120 / withFix.counts.length;
  await expect(line).toHaveAttribute('x1', String((withFix.fixAt! + 1) * barWidth));
});

test('MentionStrip_WhenNoFix_ShouldDrawNoLine', async ({ mount, page }) => {
  await mount(<MentionStrip strip={withoutFix} sprintNames={sprintNames} />);
  await expect(page.getByTestId(ids.retroMentionStrip).locator('line')).toHaveCount(0);
});
