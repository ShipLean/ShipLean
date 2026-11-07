import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { realtimeTestIds } from '../../features/realtime/testIds';
import type { PageShot } from './shoot';

// Whole pages on a phone (390×844, touch) in both themes: the phone twin of pageShots. Call it from a *.phone.spec.tsx,
// which only the CT `phone` project runs; that project sets the viewport, so this never resizes it and fails first if
// the spec runs anywhere else. Same PageShot entries and the same CI-only comparison as shoot.tsx; regenerate with
// src/ShipLean.Client/scripts/update-page-shots.sh <spec path>.

const THEMES = ['light', 'dark'] as const;
const PHONE_WIDTH = 390;

export function phonePageShots(shots: PageShot[]) {
  for (const shot of shots) {
    for (const theme of THEMES) {
      test(`PagePhone_WhenOpened_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
        mount,
        page,
      }) => {
        expect(page.viewportSize()?.width, 'phonePageShots runs only in the CT phone project').toBe(
          PHONE_WIDTH,
        );
        await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
        await shot.arrange(page);
        await mount(<AppRoutes />, { hooksConfig: { route: shot.route } });
        await shot.ready(page);
        if (shot.act) await shot.act(page);
        const liveDot = page.getByTestId(realtimeTestIds.liveDot);
        if (shot.shell) await expect(liveDot).toHaveAttribute('data-state', 'offline');

        await expect(page).toHaveScreenshot(`${shot.name}.${theme}.png`, {
          animations: 'disabled',
          mask: shot.shell ? [liveDot] : [],
        });
      });
    }
  }
}
