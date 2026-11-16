import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { ToastHost } from '../../components/ToastHost';
import { SignedInDevicesPanel } from './SignedInDevicesPanel';
import { devicesTestIds as ids } from './testIds';

// The Signed-in devices panel on a phone (SHIP-907, SHIP-968) with nobody signed in. Runs in the CT phone project (390×844).
// Regenerate with scripts/update-page-shots.sh src/features/devices/SignedInDevices.phone.spec.tsx.

for (const theme of ['light', 'dark'] as const) {
  test(`SignedInDevices_WhenNoPhonesOnPhone_ShouldMatchItsScreenshot_no-phones-390x844_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'runs only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await page.route('**/api/agents/mcp-url', (route) =>
      route.fulfill({ json: { mcpUrl: 'https://shiplean.larkspur.dev/mcp' } }),
    );
    await page.route('**/api/auth/device-sessions', (route) => route.fulfill({ json: [] }));
    await mount(
      <div className="p-4">
        <SignedInDevicesPanel />
        <ToastHost />
      </div>,
    );
    await expect(page.getByTestId(ids.empty)).toHaveText('No phones are signed in.');
    await expect(page.getByTestId(ids.addLine)).toContainText('shiplean.larkspur.dev');
    await expect(page.getByTestId(componentTestIds.toast)).toHaveCount(0);
    await expect(page).toHaveScreenshot(`no-phones-390x844.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
