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
