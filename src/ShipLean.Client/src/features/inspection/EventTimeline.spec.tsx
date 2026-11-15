import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { claudeRunPage, runRowDetail, stubInspection } from './ct/inspectionMocks';
import { RunBanners } from './RunBanners';
import { inspectionTestIds as ids } from './testIds';
import { TimelineHarness } from './timeline/ct/TimelineHarness';
import type { Variant } from './timeline/ct/TimelineHarness';

async function setup(page: Page, theme: 'light' | 'dark' = 'light') {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
  await stubInspection(page);
  await page.route('**/runs/*/rows/*', (route) =>
    route.fulfill({
      json: {
        ...runRowDetail,
        kind: 'event',
        name: 'codex.tool_result',
        filePath: null,
        commitSha: null,
      },
    }),
  );
}

// Each state asserts its own data, so a baseline cannot pass for the wrong state.
const assertState: Record<Variant, (page: Page) => Promise<void>> = {
  codex: async (page) => {
    await expect(page.getByTestId(ids.runRowEvent)).toHaveCount(9);
    await expect(page.getByTestId(ids.runRowDiamond)).toHaveCount(3);
    await expect(page.getByTestId(ids.runRowBar)).toHaveCount(6);
    await expect(page.getByTestId(ids.runRowError)).toHaveCount(1);
    await expect(page.getByTestId(ids.runBanner)).toContainText(
      'Codex CLI sends events, not spans',
    );
    await expect(page.getByTestId(ids.runPanelKind)).toBeVisible();
  },
  cursor: async (page) => {
    await expect(page.getByTestId(ids.runRowEvent)).toHaveCount(7);
    await expect(page.getByTestId(ids.runRowDiamond)).toHaveCount(6);
    await expect(page.getByTestId(ids.runRowBar)).toHaveCount(1);
    await expect(page.getByTestId(ids.runBanner)).toContainText('Cursor sends hook events only');
  },
  hidden: async (page) => {
    await expect(page.getByTestId(ids.runTurnPromptHidden)).toHaveCount(2);
    await expect(page.getByTestId(ids.runTurnPrompt)).toHaveCount(0);
  },
  truncated: async (page) => {
    await expect(page.getByTestId(ids.runTruncated)).toContainText(
      'Only part of this trace came back',
    );
    await expect(page.getByTestId(ids.runGrafanaLink)).toHaveAttribute(
      'href',
      'https://grafana.example.com/explore?traceId=abc',
    );
    await expect(page.getByTestId(ids.runEvents)).toHaveCount(0);
    await expect(page.getByTestId(ids.runWaterfall)).toBeVisible();
  },
};

for (const theme of ['light', 'dark'] as const) {
  for (const variant of ['codex', 'cursor', 'hidden', 'truncated'] as const) {
    test(`EventTimeline_WhenState${variant}_ShouldMatchItsScreenshot_${theme}`, async ({
      mount,
      page,
    }) => {
      await setup(page, theme);
      const c = await mount(
        <TimelineHarness variant={variant} initial={variant === 'codex' ? 'log-5' : undefined} />,
      );
      await assertState[variant](page);
      await expect(c).toHaveScreenshot(`event-timeline-${variant}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}

test('EventTimeline_WhenEventHasNoDuration_ShouldDrawADiamondAndNoBar', async ({ mount, page }) => {
  await setup(page);
  await mount(<TimelineHarness variant="codex" />);
  const prompt = page.locator('[data-row-id="log-1"]');
  await expect(prompt.getByTestId(ids.runRowDiamond)).toHaveCount(1);
  await expect(prompt.getByTestId(ids.runRowBar)).toHaveCount(0);
  const api = page.locator('[data-row-id="log-2"]');
  await expect(api.getByTestId(ids.runRowBar)).toHaveCount(1);
  await expect(api.getByTestId(ids.runRowDiamond)).toHaveCount(0);
});

test('EventTimeline_WhenEventHasDuration_ShouldEndTheBarAtTheEvent', async ({ mount, page }) => {
  await setup(page);
  await mount(<TimelineHarness variant="codex" />);
  // log-2 ends at 5 s on a 40 s axis and lasts 4.2 s: left 2 s (5%), width 4.2 s (10.5%).
  const bar = page.locator('[data-row-id="log-2"]').getByTestId(ids.runRowBar);
  await expect(bar).toHaveAttribute('style', /left: 2%; width: 10\.5%/);
});

test('EventTimeline_WhenRowClicked_ShouldSelectItAndClickAgainClears', async ({ mount, page }) => {
  await setup(page);
  await mount(<TimelineHarness variant="cursor" />);
  const row = page.locator('[data-row-id="cu-3"]');
  await row.click();
  await expect(row).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId(ids.runPanelKind)).toBeVisible();
  await row.click();
  await expect(row).toHaveAttribute('aria-selected', 'false');
});

test('RunBanners_WhenTruncated_ShouldFlagItWithTheGrafanaLink', async ({ mount, page }) => {
  await setup(page);
  await mount(<TimelineHarness variant="truncated" />);
  await expect(page.getByTestId(ids.runBanner)).toHaveAttribute('data-log-only', 'false');
  await expect(page.getByTestId(ids.runBanner)).toHaveAttribute('data-truncated', 'true');
});

test('RunBanners_WhenTracedAndComplete_ShouldRenderNothing', async ({ mount, page }) => {
  await setup(page);
  const c = await mount(<RunBanners page={claudeRunPage} />);
  await expect(page.getByTestId(ids.runBanner)).toHaveCount(0);
  await expect(c).toBeEmpty();
});
