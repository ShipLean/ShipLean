import { expect, test } from '@playwright/experimental-ct-react';
import { sprintNames, strips } from './ct/fixtures';
import { MentionStrip } from './MentionStrip';
import { retroSharedTestIds as ids } from './testIds';

const withFix = strips.find((s) => s.fixAt !== null)!;
const props = (strip: typeof withFix) => ({
  counts: strip.counts,
  fixAt: strip.fixAt,
  label: strip.theme.name,
  sprintNames,
});
const withoutFix = { ...withFix, fixAt: null };

test('MentionStrip_WhenRendered_ShouldHaveOneBarPerCount', async ({ mount, page }) => {
  await mount(<MentionStrip {...props(withFix)} />);
  await expect(page.getByTestId(ids.retroMentionStrip).locator('[data-bar]')).toHaveCount(
    withFix.counts.length,
  );
});

test('MentionStrip_WhenFixAtGiven_ShouldDrawTheLineAfterThatBar', async ({ mount, page }) => {
  await mount(<MentionStrip {...props(withFix)} width={120} />);
  const line = page.getByTestId(ids.retroMentionStrip).locator('[data-fix]');
  await expect(line).toHaveAttribute('data-fix-at', String(withFix.fixAt));
  const barWidth = 120 / withFix.counts.length;
  await expect(line).toHaveAttribute('x1', String((withFix.fixAt! + 1) * barWidth));
});

test('MentionStrip_WhenNoFix_ShouldDrawNoLine', async ({ mount, page }) => {
  await mount(<MentionStrip {...props(withoutFix)} />);
  await expect(page.getByTestId(ids.retroMentionStrip).locator('[data-fix]')).toHaveCount(0);
});
