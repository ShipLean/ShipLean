import { expect, test } from '@playwright/experimental-ct-react';
import { actionPoint, agentPoint, badPoint, goodPoint } from './fixtures';
import { PointRowActionsHarness } from './ct/PointRowActionsHarness';
import { PointRow } from './PointRow';
import { retroSharedTestIds as ids } from './testIds';

test('PointRow_WhenWentBadly_ShouldShowRootCause', async ({ mount, page }) => {
  await mount(<PointRow point={badPoint} />);
  await expect(page.getByTestId(ids.retroPointRow)).toContainText(badPoint.rootCause!);
});

test('PointRow_WhenNotWentBadly_ShouldNotShowRootCause', async ({ mount, page }) => {
  await mount(<PointRow point={{ ...goodPoint, rootCause: 'A cause that must stay hidden' }} />);
  await expect(page.getByTestId(ids.retroPointRow)).not.toContainText('Root cause');
});

test('PointRow_WhenAgentWrote_ShouldShowAgentChip', async ({ mount, page }) => {
  await mount(<PointRow point={agentPoint} />);
  await expect(page.getByTestId(ids.retroAuthorChip)).toContainText(agentPoint.author.agentName!);
});

test('PointRow_WhenPointHasAction_ShouldShowItsKeyAndState', async ({ mount, page }) => {
  await mount(<PointRow point={actionPoint} />);
  const row = page.getByTestId(ids.retroPointRow);
  await expect(row).toContainText(actionPoint.action!.key);
  await expect(row).toContainText(actionPoint.action!.state.name);
});

test('PointRow_WhenActionsGiven_ShouldRenderThemInTheFooter', async ({ mount, page }) => {
  await mount(<PointRowActionsHarness point={goodPoint} />);
  await expect(
    page.getByTestId(ids.retroPointRow).getByRole('button', { name: 'Archive point' }),
  ).toBeVisible();
});

test('PointRow_WhenNoTheme_ShouldShowUnthemed', async ({ mount, page }) => {
  await mount(<PointRow point={{ ...goodPoint, theme: null }} />);
  await expect(page.getByTestId(ids.retroThemeChip)).toHaveText('Unthemed');
});

for (const theme of ['light', 'dark'] as const) {
  test(`PointRow_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const row = await mount(<PointRow point={actionPoint} />);
    await expect(row).toHaveScreenshot(`point-row.${theme}.png`, { animations: 'disabled' });
  });
}
