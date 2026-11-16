import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { realtimeTestIds } from '../../features/realtime/testIds';
import type { Page, PageShot } from './shoot';

// Whole pages on a phone (390×844, touch) in both themes: the phone twin of pageShots. Call it from a *.phone.spec.tsx,
// which only the CT `phone` project runs; that project sets the viewport, so this never resizes it and fails first if
// the spec runs anywhere else. Same PageShot entries and the same CI-only comparison as shoot.tsx; regenerate with
// src/ShipLean.Client/scripts/update-page-shots.sh <spec path>.

const THEMES = ['light', 'dark'] as const;
const PHONE_WIDTH = 390;

/** The Live dot to mask on a shell page, or nothing when something opaque covers it: a full-screen dialog's header (the
 * item modal, the image viewer, the New action item) sits over the top bar, and Playwright paints a mask over whatever
 * is on top, so masking there would hide that header from the comparison. A translucent sheet scrim still shows the
 * dot, so it stays masked. */
export async function liveDotMask(page: Page) {
  const liveDot = page.getByTestId(realtimeTestIds.liveDot);
  const covered = await liveDot.evaluate((dot) => {
    const box = dot.getBoundingClientRect();
    if (box.width === 0) return false;
    const onTop = document.elementsFromPoint(box.x + box.width / 2, box.y + box.height / 2);
    for (const element of onTop) {
      if (dot.contains(element) || element.contains(dot)) return false;
      const style = getComputedStyle(element);
      // Computed colours are rgb(…), rgba(…, a) or a colour function ending in "/ a".
      const slash = /\/\s*([\d.]+)(%?)\s*\)$/.exec(style.backgroundColor);
      const rgba = /^rgba\(.*,\s*([\d.]+)\)$/.exec(style.backgroundColor);
      const alpha = slash
        ? Number(slash[1]) / (slash[2] ? 100 : 1)
        : rgba
          ? Number(rgba[1])
          : 1;
      if (alpha === 1 && style.opacity === '1') return true;
    }
    return false;
  });
  return covered ? [] : [liveDot];
}

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
        if (shot.shell) {
          await expect(page.getByTestId(realtimeTestIds.liveDot)).toHaveAttribute(
            'data-state',
            'offline',
          );
        }

        await expect(page).toHaveScreenshot(`${shot.name}.${theme}.png`, {
          animations: 'disabled',
          mask: shot.shell ? await liveDotMask(page) : [],
        });
      });
    }
  }
}
