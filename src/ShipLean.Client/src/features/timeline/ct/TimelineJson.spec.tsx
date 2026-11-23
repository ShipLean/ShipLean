import { expect, test } from '@playwright/experimental-ct-react';
import { timelineTestIds as ids } from '../testIds';
import { TimelineJson } from '../TimelineJson';
import { FINISHED_TIMELINE } from './stubFeatureTimeline';

test('TimelineJson_ShouldShowMcpNames', async ({ mount, page }) => {
  await mount(<TimelineJson view={FINISHED_TIMELINE} />);
  const json = page.getByTestId(ids.json);
  await expect(json).toContainText('critical_path');
  await expect(json).toContainText('changes_requested');
  await expect(json).not.toContainText('criticalPath');
  await expect(page.getByTestId(ids.jsonFieldGuide)).toContainText('feature_timeline');
});

test('TimelineJson_ShouldHighlightKeysAndStrings', async ({ mount, page }) => {
  await mount(<TimelineJson view={FINISHED_TIMELINE} />);
  const json = page.getByTestId(ids.json);
  await expect(json.locator('.json-k').first()).toBeVisible();
  await expect(json.locator('.json-s').first()).toBeVisible();
});

test('TimelineJson_WithShortJson_ShouldFillTheChartAreaHeight', async ({ mount, page }) => {
  const tiny = {
    ...FINISHED_TIMELINE,
    keyDates: [],
    gaps: [],
    criticalPath: [],
    stories: [],
    bars: [],
    edges: [],
    notStarted: [],
    links: [],
  };
  await page.setViewportSize({ width: 900, height: 1600 });
  await mount(
    <div style={{ height: 1500, display: 'flex', flexDirection: 'column' }}>
      <TimelineJson view={tiny} />
    </div>,
  );
  const pane = page.getByLabel('feature_timeline response for SHIP-99');
  await expect(pane).toContainText('critical_path');
  const section = await page.getByTestId(ids.json).boundingBox();
  const box = await pane.boundingBox();
  expect(section?.height ?? 0).toBeGreaterThanOrEqual(1500);
  // The pane itself reaches the bottom of the area (section padding is 16px).
  expect((box?.y ?? 0) + (box?.height ?? 0)).toBeGreaterThanOrEqual(1500 - 16 - 1);
});

test('TimelineJson_Copy_ShouldCopyTheJson', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await mount(<TimelineJson view={FINISHED_TIMELINE} />);
  await page.getByTestId(ids.jsonCopy).click();
  await expect(page.getByTestId(ids.jsonCopy)).toHaveText('Copied');
  const copied = await page.evaluate(() => navigator.clipboard.readText());
  const parsed = JSON.parse(copied) as { critical_path: string[]; feature: { key: string } };
  expect(parsed.feature.key).toBe('SHIP-99');
  expect(parsed.critical_path).toEqual(FINISHED_TIMELINE.criticalPath);
});
