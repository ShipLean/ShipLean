import { expect, test } from '@playwright/experimental-ct-react';
import settings from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings.json' with { type: 'json' };
import type { YouTubeProjectDto } from '@shiplean/core/api/demos';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { demosTestIds as ids } from '../../demos/testIds';
import { SettingsDemosHarness } from './ct/SettingsDemosHarness';

// Project settings, Demos, on a phone (SHIP-732 P8, L732c): a form with its own full-width Save, the brand fields
// stacked, and YouTube's connected state with its stacked Disconnect confirmation. Regenerate with
// scripts/update-page-shots.sh src/features/settings/demos/SettingsDemos.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const admin = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;
const hooks = { hooksConfig: { route: '/p/SHIP/settings/demos' } };
const connected: YouTubeProjectDto = {
  status: 'connected',
  channelTitle: 'Storefront Official',
  visibility: 'unlisted',
  checkedLabel: 'Checked 4 Nov 2026, 14:02',
  refusal: null,
};

for (const theme of THEMES) {
  test(`DemosPhone_WhenOpened_ShouldMatchItsScreenshot_settings-demos-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await page.route('**/api/projects/*/demo-settings', (route) =>
      route.fulfill({ json: settings }),
    );
    await mount(<SettingsDemosHarness project={admin} />, hooks);
    const panel = page.getByTestId(ids.demoSettingsPanel);
    await expect(panel).toBeVisible();

    const environment = page.getByTestId(ids.demoSettingsEnvironment);
    expect(await environment.evaluate((el) => getComputedStyle(el).fontSize)).toBe('16px');
    const frame = (await panel.boundingBox())!;
    const save = (await page.getByTestId(ids.demoSettingsSave).boundingBox())!;
    expect(save.height).toBeGreaterThanOrEqual(32);
    expect(save.width).toBeGreaterThan(frame.width - 40);
    const tokens = (await page.getByTestId(ids.demoTokensSave).boundingBox())!;
    expect(tokens.height).toBeGreaterThanOrEqual(32);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= 390)).toBe(true);

    await expect(page).toHaveScreenshot(`settings-demos-phone.${theme}.png`, {
      animations: 'disabled',
      fullPage: true,
    });
  });

  test(`DemosPhone_WhenYouTubeConnected_ShouldMatchItsScreenshot_settings-demos-youtube-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await page.route('**/api/projects/*/demo-settings', (route) =>
      route.fulfill({ json: { ...settings, youTube: connected } }),
    );
    await mount(<SettingsDemosHarness project={admin} />, hooks);
    const youtube = page.getByTestId(ids.youtubePanel);
    await expect(page.getByTestId(ids.youtubeConnected)).toBeVisible();

    const visibility = page.getByTestId(ids.youtubeVisibility);
    const segments = await visibility.locator('> span').evaluateAll((els) =>
      els.map((el) => {
        const r = el.getBoundingClientRect();
        return { x: r.x, y: Math.round(r.y), width: Math.round(r.width), height: r.height };
      }),
    );
    expect(segments).toHaveLength(3);
    for (const segment of segments) expect(segment.height).toBeGreaterThanOrEqual(32);
    // Side by side: one row.
    expect(new Set(segments.map((s) => s.y)).size).toBe(1);
    // The untouched connected panel, before any tap changes the visibility or opens the disconnect confirm.
    await expect(youtube).toHaveScreenshot(`settings-demos-youtube-phone.${theme}.png`, {
      animations: 'disabled',
    });
    // A tap near a segment's bottom-right corner, away from the radio and its text, chooses it.
    await visibility.scrollIntoViewIfNeeded();
    const last = (await visibility.locator('> span').nth(2).boundingBox())!;
    await page.route('**/api/projects/*/youtube/check', (route) =>
      route.fulfill({ json: { ...connected, visibility: 'private' } }),
    );
    await page.mouse.click(last.x + last.width - 4, last.y + last.height - 4);
    await expect(visibility.getByLabel('Private', { exact: true })).toBeChecked();

    await page.getByTestId(ids.youtubeDisconnect).click();
    const confirm = (await page.getByTestId(ids.youtubeDisconnect).boundingBox())!;
    const keep = (await page.getByRole('button', { name: 'Keep it' }).boundingBox())!;
    expect(confirm.height).toBeGreaterThanOrEqual(32);
    expect(keep.height).toBeGreaterThanOrEqual(32);
    // Stacked: one above the other, the same width.
    expect(keep.y).toBeGreaterThan(confirm.y + confirm.height - 1);
    expect(Math.round(keep.width)).toBe(Math.round(confirm.width));
    expect(confirm.width).toBeGreaterThan((await youtube.boundingBox())!.width - 40);
  });
}

// ND3a-11 (SHIP-1809 P7): the visibility is one row of plain radios packed to the left, not three boxed cards with notes.
test('DemosPhone_WhenYouTubeConnected_ShouldShowVisibilityAsOneRowOfPlainRadios', async ({
  mount,
  page,
}) => {
  await page.route('**/api/projects/*/demo-settings', (route) =>
    route.fulfill({ json: { ...settings, youTube: connected } }),
  );
  await mount(<SettingsDemosHarness project={admin} />, hooks);
  const visibility = page.getByTestId(ids.youtubeVisibility);
  await expect(visibility).toBeVisible();
  const segments = visibility.locator('> span');
  await expect(segments).toHaveCount(3);
  await expect(segments.first()).toHaveCSS('border-top-width', '0px');
  const [group, first, last] = await Promise.all([
    visibility.boundingBox(),
    segments.first().boundingBox(),
    segments.last().boundingBox(),
  ]);
  if (!group || !first || !last) throw new Error('visibility not drawn');
  // One line, and the three take well under the row: no notes beside each radio and no equal thirds.
  expect(Math.abs(last.y - first.y)).toBeLessThanOrEqual(1);
  expect(last.x + last.width - first.x).toBeLessThan(group.width * 0.8);
  await expect(visibility.getByLabel('Unlisted', { exact: true })).toBeChecked();
});
