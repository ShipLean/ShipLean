import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import settings from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings.json' with { type: 'json' };
import branded from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings-branded.json' with { type: 'json' };
import type { ProjectDto } from '../../../api/projects';
import { componentTestIds } from '../../../components/testIds';
import { demosTestIds as ids } from '../../demos/testIds';
import { SettingsDemosHarness } from './ct/SettingsDemosHarness';

const admin = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;
const member = { ...admin, role: 'member' } as ProjectDto;
const hooks = { hooksConfig: { route: '/p/SHIP/settings/demos' } };

async function serve(page: Page, body: unknown) {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: body }));
}

test('Logo_WhenNone_ShouldShowShipLeansAsTheDefault', async ({ mount, page }) => {
  await serve(page, settings);
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  await expect(page.getByTestId(ids.demoLogoDefault)).toHaveText('Default');
  const preview = page.getByTestId(ids.demoLogoPreview);
  await expect(preview).toHaveJSProperty('tagName', 'IMG');
  // A static client asset, not the project's logo route; it loads, and has the 640 x 128 size of ShipLean's own logo art.
  await expect(preview).not.toHaveAttribute('src', /demo-brand/);
  await expect(preview).toHaveJSProperty('naturalWidth', 640);
  await expect(preview).toHaveJSProperty('naturalHeight', 128);
  await expect(page.getByTestId(ids.demoLogoRemove)).toHaveCount(0);
});

test('Logo_WhenSet_ShouldPreviewItThroughAnImg', async ({ mount, page }) => {
  await serve(page, branded);
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  const preview = page.getByTestId(ids.demoLogoPreview);
  await expect(preview).toHaveJSProperty('tagName', 'IMG');
  await expect(preview).toHaveAttribute('src', branded.brand.logoUrl);
  await expect(page.getByTestId(ids.demoLogoDefault)).toHaveCount(0);
  await expect(page.getByTestId(ids.demoLogoRemove)).toBeVisible();
});

// Layout: each logo shape at both desktop widths. The tab is taller than the window, so the buttons are scrolled into view
// first and must then sit inside innerHeight; the page itself must not scroll sideways.
for (const viewportWidth of [1280, 1440]) {
  for (const [name, width, height] of [
    ['VeryWide', 4000, 40],
    ['VeryTall', 40, 4000],
  ] as const) {
    test(`Logo_When${name}At${viewportWidth}_ShouldStayInsideItsBox`, async ({ mount, page }) => {
      const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="${width}" height="${height}" fill="#c2410c"/></svg>`;
      await page.setViewportSize({ width: viewportWidth, height: 720 });
      await page.route('**/demo-brand/logo*', (route) =>
        route.fulfill({ contentType: 'image/svg+xml', body: svg }),
      );
      await serve(page, branded);
      await mount(<SettingsDemosHarness project={admin} />, hooks);

      const preview = page.getByTestId(ids.demoLogoPreview);
      await expect(preview).toHaveJSProperty('naturalWidth', width);
      const box = (await preview.boundingBox())!;
      expect(box.width).toBeLessThanOrEqual(176);
      expect(box.height).toBeLessThanOrEqual(56);
      expect(box.width).toBeGreaterThan(0);

      for (const id of [ids.demoLogoRemove, ids.demoTokensSave]) {
        const button = page.getByTestId(id);
        await button.scrollIntoViewIfNeeded();
        const at = (await button.boundingBox())!;
        const inner = await page.evaluate(() => window.innerHeight);
        expect(at.y).toBeGreaterThanOrEqual(0);
        expect(at.y + at.height).toBeLessThanOrEqual(inner);
      }
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      );
      expect(overflow).toBeLessThanOrEqual(0);
    });
  }
}

test('Logo_WhenUploadChosenByKeyboard_ShouldPost', async ({ mount, page }) => {
  let puts = 0;
  await serve(page, settings);
  await page.route('**/api/projects/*/demo-brand/logo', (route) => {
    puts++;
    return route.fulfill({ json: branded.brand });
  });
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  const input = page.getByTestId(ids.demoLogoFile);
  await input.focus();
  await expect(input).toBeFocused();
  const chooser = page.waitForEvent('filechooser');
  await page.keyboard.press('Space');
  await (
    await chooser
  ).setFiles({ name: 'logo.png', mimeType: 'image/png', buffer: Buffer.from('png') });

  await expect(page.getByTestId(ids.demoLogoRemove)).toBeVisible();
  expect(puts).toBe(1);
});

test('Logo_WhenUploadRefused_ShouldShowWhy', async ({ mount, page }) => {
  const message = 'A logo must be a PNG, JPEG, WebP or SVG image.';
  let puts = 0;
  await serve(page, settings);
  await page.route('**/api/projects/*/demo-brand/logo', (route) => {
    puts++;
    return route.fulfill({
      status: 400,
      json: { error: message, code: 'validation', field: 'logo' },
    });
  });
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  await page.getByTestId(ids.demoLogoFile).setInputFiles({
    name: 'brand-guidelines.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4'),
  });

  await expect(page.getByTestId(ids.demoLogoError)).toHaveText(message);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(message);
  await expect(page.getByTestId(ids.demoLogoDefault)).toBeVisible();
  expect(puts).toBe(1);
});

test('Logo_WhenRemoved_ShouldCallAndShowTheDefault', async ({ mount, page }) => {
  let deletes = 0;
  await serve(page, branded);
  await page.route('**/api/projects/*/demo-brand/logo', (route) => {
    if (route.request().method() !== 'DELETE') return route.continue();
    deletes++;
    return route.fulfill({ json: { ...settings.brand, tokens: branded.brand.tokens } });
  });
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  await page.getByTestId(ids.demoLogoRemove).click();

  await expect(page.getByTestId(ids.demoLogoDefault)).toBeVisible();
  await expect(page.getByTestId(ids.demoLogoRemove)).toHaveCount(0);
  expect(deletes).toBe(1);
});

test('Tokens_ShouldListTheDefaultNames', async ({ mount, page }) => {
  // Served names and values that differ from the contract's, so a hardcoded list cannot pass.
  const defaultTokens = [
    ...settings.brand.defaultTokens.map((token) =>
      token.name === '--color-accent' ? { ...token, value: '#123456' } : token,
    ),
    { name: '--color-extra', value: '9px' },
  ];
  await serve(page, { ...settings, brand: { ...settings.brand, defaultTokens } });
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  const rows = defaultTokens.map((token) => `${token.name}${token.value}`).join('');
  await expect(page.getByTestId(ids.demoTokensHints)).toHaveText(
    new RegExp(`^The seven names slides use, with ShipLean's values${rows}One per line`),
  );
});

test('Tokens_ShouldSayOtherNamesAreSavedAndNotUsed', async ({ mount, page }) => {
  await serve(page, settings);
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  await expect(page.getByTestId(ids.demoTokensHints)).toContainText(
    'Other names are saved and not used.',
  );
});

test('Tokens_WhenRefused_ShouldShowTheLine', async ({ mount, page }) => {
  const message = 'Not saved. Line 3: --color-bg has a value that is not allowed.';
  await serve(page, settings);
  await page.route('**/api/projects/*/demo-brand/tokens', (route) =>
    route.fulfill({ status: 400, json: { error: message, code: 'validation', field: 'tokens' } }),
  );
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  await page.getByTestId(ids.demoTokens).fill('--color-bg: url(https://x.example/p.png);');
  await page.getByTestId(ids.demoTokensSave).click();

  await expect(page.getByTestId(ids.demoTokensError)).toHaveText(message);
  await expect(page.getByTestId(ids.demoTokens)).toHaveValue(
    '--color-bg: url(https://x.example/p.png);',
  );
});

test('Tokens_WhenSaved_ShouldCallOnceAndToastOnFailure', async ({ mount, page }) => {
  const puts: unknown[] = [];
  let fail = true;
  await serve(page, settings);
  await page.route('**/api/projects/*/demo-brand/tokens', (route) => {
    puts.push(route.request().postDataJSON());
    return fail
      ? route.fulfill({
          status: 400,
          json: { error: 'Line 1 is wrong.', code: 'validation', field: 'tokens' },
        })
      : route.fulfill({ json: { ...settings.brand, tokens: '--color-accent: #0b7a53;' } });
  });
  await mount(<SettingsDemosHarness project={admin} />, hooks);

  await page.getByTestId(ids.demoTokens).fill('--color-accent: #0b7a53;');
  await page.getByTestId(ids.demoTokensSave).click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Line 1 is wrong.');
  expect(puts).toEqual([{ tokens: '--color-accent: #0b7a53;' }]);

  fail = false;
  await page.getByTestId(ids.demoTokensSave).click();
  await expect(page.getByTestId(ids.demoTokensError)).toHaveCount(0);
  expect(puts).toHaveLength(2);
  await expect(page.getByTestId(ids.demoTokens)).toHaveValue('--color-accent: #0b7a53;');
});

test('ReadOnly_WhenMember_ShouldHideTheActions', async ({ mount, page }) => {
  await serve(page, branded);
  await mount(<SettingsDemosHarness project={member} />, hooks);

  await expect(page.getByTestId(ids.demoLogoPreview)).toBeVisible();
  await expect(page.getByTestId(ids.demoTokens)).toHaveValue(branded.brand.tokens);
  await expect(page.getByTestId(ids.demoTokens)).not.toBeEditable();
  await expect(page.getByTestId(ids.demoLogoFile)).toHaveCount(0);
  await expect(page.getByTestId(ids.demoLogoRemove)).toHaveCount(0);
  await expect(page.getByTestId(ids.demoTokensSave)).toHaveCount(0);
});
