import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT, planningView } from '../../features/backlog/ct/fixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { PlanningMode } from '../../features/backlog/planning/PlanningMode';
import { backlogTestIds as ids } from '../../features/backlog/testIds';
import { touchTestIds } from '../../touch/testIds';

// Planning on a phone (SHIP-716, L716d) as element shots of this task's regions only, so the sibling tasks' merges don't
// move them: a sprint's ⋯ sheet, ⋯ More, and a row lifted over a sprint header. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/backlogPlanning.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const saved = { collapsedSections: [], forecast: true };

for (const theme of THEMES) {
  test(`BacklogPlanningPhone_WhenSprintMoreOpened_ShouldMatchItsScreenshot_backlogs-planning-section_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBacklog(page, planningView(), { saved });
    await mount(<PlanningMode project={PROJECT} />, route);
    const block = page
      .getByTestId(ids.planningSection)
      .filter({ has: page.getByTestId(ids.planningSectionTitle).getByText('Sprint 25') });
    await block.getByTestId(touchTestIds.moreButton).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toBeVisible();

    await expect(sheet).toHaveScreenshot(`backlogs-planning-section.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BacklogPlanningPhone_WhenMoreOpened_ShouldMatchItsScreenshot_backlogs-planning-more_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBacklog(page, planningView(), { saved });
    await mount(<PlanningMode project={PROJECT} />, route);
    await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toBeVisible();

    await expect(sheet).toHaveScreenshot(`backlogs-planning-more.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BacklogPlanningPhone_WhenRowLifted_ShouldMatchItsScreenshot_backlogs-planning-drag_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await page.clock.install({ time: 0 });
    await stubBacklog(page, planningView(), { saved });
    await mount(<PlanningMode project={PROJECT} />, route);
    const cdp = await page.context().newCDPSession(page);
    const send = (type: 'touchStart' | 'touchMove' | 'touchEnd', x = -1, y = -1) =>
      cdp.send('Input.dispatchTouchEvent', {
        type,
        touchPoints: type === 'touchEnd' ? [] : [{ x, y }],
      });
    const handle = page
      .getByTestId(ids.row)
      .and(page.locator('[data-key="SHIP-170"]'))
      .getByTestId(ids.touchHandle);
    const from = (await handle.boundingBox())!;
    const header = page
      .getByTestId(ids.planningSection)
      .filter({ has: page.getByTestId(ids.planningSectionTitle).getByText('Sprint 25') });
    const target = (await header.boundingBox())!;
    const x = Math.round(from.x + from.width / 2);
    const y = Math.round(from.y + from.height / 2);

    await send('touchStart', x, y);
    await page.clock.runFor(500);
    for (let i = 1; i <= 10; i++) {
      await send('touchMove', x + 150, Math.round(y + ((target.y + 30 - y) * i) / 10));
    }
    await expect(header).toHaveAttribute('data-over', 'true');

    await expect(page.getByTestId(ids.planningTouch)).toHaveScreenshot(
      `backlogs-planning-drag.${theme}.png`,
      { animations: 'disabled' },
    );
    await send('touchEnd');
  });
}
