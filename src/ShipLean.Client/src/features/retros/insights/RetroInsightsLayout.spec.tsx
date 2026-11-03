import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import type { RetroInsightsDto } from '../../../api/retros';
import { insightsWeeks, retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const many = (base: RetroInsightsDto, columns: number): RetroInsightsDto => ({
  ...base,
  columns: Array.from({ length: columns }, (_, i) => ({
    ...base.columns[i % base.columns.length],
    key: `k${i}`,
    label: base.mode === 'week' ? `${i + 1} Oct` : `Sprint ${i + 1}`,
  })),
  themes: Array.from({ length: 30 }, (_, i) => {
    const source = base.themes[i % base.themes.length];
    return {
      ...source,
      theme: { ...source.theme, id: `theme-${i}`, name: `${source.theme.name} ${i + 1}` },
      counts: Array.from({ length: columns }, (_, c) => (i + c) % 4),
    };
  }),
});

async function overflow(page: Page) {
  return page.evaluate(() => {
    const at = (id: string) =>
      document.querySelector(`[data-testid="${id}"]`)!.getBoundingClientRect();
    const range = at('retro-insights-range');
    const merge = at('retro-merge-themes');
    const panel = at('retro-insights-panel');
    return {
      horizontal: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      width: window.innerWidth,
      height: window.innerHeight,
      panelRight: panel.right,
      mergeRight: merge.right,
      barTop: Math.min(range.top, merge.top),
      barBottom: Math.max(range.bottom, merge.bottom),
    };
  });
}

for (const width of [1280, 1440]) {
  for (const scheme of ['light', 'dark'] as const) {
    for (const mode of ['week', 'sprint'] as const) {
      test(`RetroInsights_When30ThemesAt${width}${scheme}${mode}_ShouldKeepTheBarInViewAndNotScrollSideways`, async ({
        mount,
        page,
      }) => {
        await page.setViewportSize({ width, height: 800 });
        await page.emulateMedia({ colorScheme: scheme, reducedMotion: 'reduce' });
        await stubShell(page);
        await stubRetros(page);
        const answer = mode === 'week' ? many(insightsWeeks, 12) : many(retroInsights, 6);
        await page.route(
          (url) => /\/retros\/insights$/.test(url.pathname),
          (route) => route.fulfill({ json: { ...answer, range: mode === 'week' ? 'w12' : '6' } }),
        );
        await mount(<AppRoutes />, {
          hooksConfig: { route: `/p/SHIP/retros/insights?range=${mode === 'week' ? 'w12' : '6'}` },
        });
        await expect(page.getByTestId(ids.retroRecurringRow)).toHaveCount(30);
        const last = page.getByTestId(ids.retroRecurringRow).last();
        await last.scrollIntoViewIfNeeded();
        const found = await overflow(page);
        expect(found.horizontal).toBeLessThanOrEqual(0);
        expect(found.panelRight).toBeLessThanOrEqual(found.width);
        expect(found.mergeRight).toBeLessThanOrEqual(found.width);
        expect(found.barTop).toBeGreaterThanOrEqual(0);
        expect(found.barBottom).toBeLessThanOrEqual(found.height);
        await expect(page.getByTestId(ids.retroInsightsUnthemed)).toBeAttached();
        await page.screenshot({ path: `/tmp/claude-1000/layout-${mode}-${width}-${scheme}.png` });
      });
    }
  }
}
