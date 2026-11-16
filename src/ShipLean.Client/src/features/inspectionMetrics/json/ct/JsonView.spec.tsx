import { expect, test } from '@playwright/experimental-ct-react';
import { projectDto } from '../../../../pages/pagesFixtures';
import { stubView } from '../../ct/stubInspectionMetrics';
import { DEFAULT_INSPECTION_QUERY, type InspectionQuery } from '../../inspectionMetricsQuery';
import { inspectionMetricsTestIds as ids } from '../../testIds';
import { JsonView } from '../JsonView';

const keysOf = (value: unknown): string[] =>
  Array.isArray(value)
    ? value.flatMap(keysOf)
    : value !== null && typeof value === 'object'
      ? Object.entries(value).flatMap(([k, v]) => [k, ...keysOf(v)])
      : [];

for (const scheme of ['light', 'dark'] as const) {
  test(`JsonView_WhenRendered_ShouldShowTheMcpFormInSnakeCase_${scheme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: 'reduce' });
    await mount(
      <JsonView
        project={projectDto('SHIP')}
        view={stubView()}
        query={{ ...DEFAULT_INSPECTION_QUERY, view: 'json' }}
        onQueryChange={() => undefined}
      />,
    );
    const text = await page.getByTestId(ids.jsonBody).textContent();
    const json = JSON.parse(text ?? '') as Record<string, unknown>;
    expect(keysOf(json).filter((k) => /[A-Z]/.test(k))).toEqual([]);
    expect(Object.keys(json)).toContain('generated_at');
    expect(Object.keys(json)).toContain('period_detail');
    expect(text).toContain('"ended_by": "last_commit"');
    await expect(page.getByTestId(ids.json).getByRole('heading', { level: 2 })).toHaveText(
      'JSON for agents',
    );
  });
}

test('JsonView_WhenBackClicked_ShouldAskForTheOverview', async ({ mount, page }) => {
  const patches: Partial<InspectionQuery>[] = [];
  await mount(
    <JsonView
      project={projectDto('SHIP')}
      view={stubView()}
      query={{ ...DEFAULT_INSPECTION_QUERY, view: 'json' }}
      onQueryChange={(patch) => patches.push(patch)}
    />,
  );
  await page.getByTestId(ids.json).getByRole('button', { name: '← Overview' }).click();
  await expect.poll(() => patches).toEqual([{ view: 'overview' }]);
});
