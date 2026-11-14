import { expect, test } from '@playwright/experimental-ct-react';
import { EPIC_ROWS, FEATURE_ROWS, levelView } from '../../features/backlog/ct/levelFixtures';
import { LevelHarness } from '../../features/backlog/ct/LevelHarness';
import { PROJECT } from '../../features/backlog/ct/fixtures';
import { stubLevel } from '../../features/backlog/ct/stubLevels';
import { backlogTestIds as ids } from '../../features/backlog/testIds';

// The Features and Epics backlogs on a phone (SHIP-716 D1, D8), as element shots of the rows list only, so the toolbar
// task's merges don't move them. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/backlogLevels.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;

const SHOTS = [
  { name: 'backlogs-features-rows', level: 'features', rows: FEATURE_ROWS },
  { name: 'backlogs-epics-rows', level: 'epics', rows: EPIC_ROWS },
] as const;

for (const shot of SHOTS) {
  for (const theme of THEMES) {
    test(`BacklogLevelsPhone_WhenOpened_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
      mount,
      page,
    }) => {
      expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await stubLevel(page, levelView(shot.level, shot.rows));
      const screen = await mount(<LevelHarness project={PROJECT} level={shot.level} />, {
        hooksConfig: { route: `/p/SHIP/backlogs/${shot.level}` },
      });
      await expect(screen.getByTestId(ids.row)).toHaveCount(shot.rows.length);

      await expect(screen.getByTestId(ids.touchRowList)).toHaveScreenshot(
        `${shot.name}.${theme}.png`,
        { animations: 'disabled' },
      );
    });
  }
}
