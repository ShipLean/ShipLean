import { expect, test } from '@playwright/experimental-ct-react';
import { retroThemes, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { MergeThemes } from './MergeThemes';

const [a, b, c] = retroThemes.themes;

test('MergeThemes_WhenTwoSourcesAndATargetChosen_ShouldPostTheMerge', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<MergeThemes project={insightsProject} />);
  await page.getByTestId(ids.retroMergeThemes).click();
  await expect(page.getByTestId(ids.retroMergeDialog)).toBeVisible();
  const sources = page.getByTestId(ids.retroMergeSource);
  await sources.and(page.locator(`[data-theme-id="${a.theme.id}"]`)).check();
  await sources.and(page.locator(`[data-theme-id="${b.theme.id}"]`)).check();
  await page.getByTestId(ids.retroMergeTarget).selectOption(c.theme.id);
  await expect(page.getByTestId(ids.retroMergePreview)).toHaveAttribute(
    'data-combined',
    String(a.pointCount + b.pointCount + c.pointCount),
  );
  await page.getByTestId(ids.retroMergeSave).click();
  await expect.poll(() => stub.writes().length).toBe(1);
  const write = stub.writes()[0];
  expect(write.method).toBe('POST');
  expect(write.path).toBe(`/api/projects/${insightsProject.id}/retros/themes/merge`);
  expect(write.body).toEqual({ sourceIds: [a.theme.id, b.theme.id], targetId: c.theme.id });
  await expect(page.getByTestId(ids.retroMergeDialog)).toHaveCount(0);
});

test('MergeThemes_WhenTargetPicked_ShouldNotAlsoBeASource', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<MergeThemes project={insightsProject} />);
  await page.getByTestId(ids.retroMergeThemes).click();
  const source = page
    .getByTestId(ids.retroMergeSource)
    .and(page.locator(`[data-theme-id="${a.theme.id}"]`));
  await source.check();
  await page.getByTestId(ids.retroMergeTarget).selectOption(a.theme.id);
  await expect(source).toBeDisabled();
  await expect(source).not.toBeChecked();
  await expect(page.getByTestId(ids.retroMergeSave)).toBeDisabled();
});
