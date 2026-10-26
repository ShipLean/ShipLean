import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { realtimeTestIds } from '../../features/realtime/testIds';

// Whole pages (shell + screen) at the reference designs' size (design/screens, 1440×900) in both themes. Baselines are
// generated inside the CI image (mcr.microsoft.com/playwright:v1.62.0-resolute): a run on another machine renders fonts
// differently, so outside CI the comparison is skipped (ignoreSnapshots). Regenerate with
// src/ShipLean.Client/scripts/update-page-shots.sh [spec…]; docs/testing.md has the rest.

export type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

export interface PageShot {
  /** The mockup's name (design/screens/<name>.html, or the variant in design/screenshots.sh). Names the PNGs. */
  name: string;
  /** The SPA route to open, e.g. `/p/SHIP/backlogs/stories`. */
  route: string;
  /** Registers the page's API stubs. Call `stubShell` first for shell pages: stubs registered later win. */
  arrange: (page: Page) => Promise<void>;
  /** Resolves once the page's own data is on screen (a data-testid locator, never a sleep). */
  ready: (page: Page) => Promise<void>;
  /** Optional: opens a dialog, switches a tab or toggles tree mode, then waits for the result before returning. */
  act?: (page: Page) => Promise<void>;
  /** True for pages inside the project shell: the helper waits for the Live dot (Offline: CT has no hub) and masks it. */
  shell: boolean;
}

const THEMES = ['light', 'dark'] as const;

export function pageShots(shots: PageShot[]) {
  for (const shot of shots) {
    for (const theme of THEMES) {
      test(`Page_WhenOpened_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
        mount,
        page,
      }) => {
        await page.setViewportSize({ width: 1440, height: 900 });
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
