import { expect, test } from '@playwright/experimental-ct-react';
import { retrosTestIds as ids } from '../../testIds';
import { StatusChip } from '../recurring/StatusChip';
import { VerdictChip } from '../recurring/VerdictChip';
import { LastChangeHeading } from './LastChangeHeading';
import { NoSprintTip } from './NoSprintTip';
import { StatusHeading } from './StatusHeading';
import { UnthemedTip } from './UnthemedTip';

test('Tips_WhenStatusHeadingFocused_ShouldExplainAllFour', async ({ mount, page }) => {
  await mount(
    <table>
      <thead>
        <tr>
          <StatusHeading className="" mode="sprint" />
        </tr>
      </thead>
    </table>,
  );
  await page.getByTestId(ids.retroTipStatusHead).getByText('Status').focus();
  const tip = page.getByRole('tooltip');
  for (const term of ['Recurring', 'Being addressed', 'Resolved', 'Quiet']) {
    await expect(tip.getByText(term, { exact: true })).toBeVisible();
  }
});

test('Tips_WhenModeChanges_ShouldWordRecurringByIt', async ({ mount, page }) => {
  const head = (mode: 'sprint' | 'week') => (
    <table>
      <thead>
        <tr>
          <StatusHeading className="" mode={mode} />
        </tr>
      </thead>
    </table>
  );
  const view = await mount(head('sprint'));
  await page.getByTestId(ids.retroTipStatusHead).getByText('Status').focus();
  await expect(page.getByRole('tooltip')).toContainText('3 of the last 4 sprints');
  await view.update(head('week'));
  await page.getByTestId(ids.retroTipStatusHead).getByText('Status').blur();
  await page.getByTestId(ids.retroTipStatusHead).getByText('Status').focus();
  await expect(page.getByRole('tooltip')).toContainText('3 of the last 6 weeks');
});

test('Tips_WhenChipFocused_ShouldNameItsMeaning', async ({ mount, page }) => {
  await mount(
    <>
      <StatusChip status="quiet" mode="sprint" />
      <VerdictChip verdict="noEffect" mode="week" />
    </>,
  );
  await page.keyboard.press('Tab');
  await expect(page.getByRole('tooltip')).toHaveText(
    'Not recurring, with no open action item and no fix that helped.',
  );
  await page.keyboard.press('Tab');
  await expect(page.getByRole('tooltip')).toHaveText(
    'Mentions per week after the fix are the same or higher than before.',
  );
});

test('Tips_WhenFocusedByKeyboard_ShouldShowAndHideTheTip', async ({ mount, page }) => {
  await mount(<LastChangeHeading className="" mode="sprint" />);
  await expect(page.getByRole('tooltip')).toHaveCount(0);
  await page.keyboard.press('Tab');
  await expect(page.getByRole('tooltip')).toContainText('after its last action item closed');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('tooltip')).toHaveCount(0);
});

test('Tips_WhenOnTheLastRowAndRightColumn_ShouldStayInsideTheWindow', async ({ mount, page }) => {
  await page.setViewportSize({ width: 700, height: 400 });
  await mount(
    <div style={{ position: 'relative', height: 380, width: 690 }}>
      <div style={{ position: 'absolute', right: 0, bottom: 0 }}>
        <StatusChip status="recurring" mode="sprint" />
      </div>
    </div>,
  );
  await page.keyboard.press('Tab');
  const tip = page.getByRole('tooltip');
  await expect(tip).toBeVisible();
  const box = (await tip.boundingBox())!;
  expect(box.x).toBeGreaterThanOrEqual(0);
  expect(box.y).toBeGreaterThanOrEqual(0);
  expect(box.x + box.width).toBeLessThanOrEqual(700);
  expect(box.y + box.height).toBeLessThanOrEqual(400);
});

test('Tips_WhenNoSprintAndUnthemedFocused_ShouldExplainThem', async ({ mount, page }) => {
  await mount(
    <>
      <NoSprintTip />
      <UnthemedTip />
    </>,
  );
  await page.getByTestId(ids.retroTipNosprint).focus();
  await expect(page.getByRole('tooltip')).toContainText('no sprint. They count in Total');
  await page.getByTestId(ids.retroTipUnthemed).focus();
  await expect(page.getByRole('tooltip')).toContainText('Give them one in Search');
});
