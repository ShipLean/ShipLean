import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import type { RetroInsightsDto } from '../../../api/retros';
import { insightsWeeks, retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const MONDAY = Date.UTC(2026, 9, 26);
const WEEK_MS = 7 * 24 * 3600 * 1000;
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** A valid K4 answer with `size` columns: keys are sprint ids or Mondays, one current column, every count parallel. */
const column = (mode: RetroInsightsDto['mode'], index: number, size: number) => {
  const current = index === size - 1;
  if (mode === 'sprint') {
    return {
      key: `00000004-0000-4000-8001-${String(index + 1).padStart(12, '0')}`,
      label: `Sprint ${index + 1}`,
      kind: 'sprint' as const,
      isCurrent: current,
    };
  }
  const monday = new Date(MONDAY - (size - 1 - index) * WEEK_MS);
  return {
    key: monday.toISOString().slice(0, 10),
    label: `${monday.getUTCDate()} ${MONTHS[monday.getUTCMonth()]}`,
    kind: 'week' as const,
    isCurrent: current,
  };
};

const many = (base: RetroInsightsDto, size: number): RetroInsightsDto => {
  const columns = Array.from({ length: size }, (_, i) => column(base.mode, i, size));
  const series = (seed: number) => Array.from({ length: size }, (_, c) => (seed + c) % 4);
  const sum = (counts: number[]) => counts.reduce((a, b) => a + b, 0);
  const noSprint = base.mode === 'sprint' ? 2 : 0;
  const unthemedCounts = series(1);
  return {
    ...base,
    columns,
    sprints:
      base.mode === 'sprint'
        ? columns.map((c, i) => ({
            ...base.sprints[i % base.sprints.length],
            id: c.key,
            name: c.label,
          }))
        : [],
    unthemed: {
      counts: unthemedCounts,
      noSprint,
      total: sum(unthemedCounts) + noSprint,
    },
    themes: Array.from({ length: 30 }, (_, i) => {
      const source = base.themes[i % base.themes.length];
      const counts = series(i);
      return {
        ...source,
        theme: { ...source.theme, id: `theme-${i}`, name: `${source.theme.name} ${i + 1}` },
        counts,
        noSprint,
        total: sum(counts) + noSprint,
      };
    }),
    keepsWorking: base.keepsWorking.map((source, i) => {
      const counts = series(i + 2);
      return { ...source, counts, noSprint, total: sum(counts) + noSprint };
    }),
    impact: base.impact.map((card, i) => ({
      ...card,
      counts: series(i),
      closedAt: card.closedAt < 0 ? -1 : Math.min(card.closedAt, size - 1),
    })),
  };
};

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
