import { expect, test } from '@playwright/experimental-ct-react';
import { RailJsonGuide } from '../rail/RailJsonGuide';
import { timelineTestIds as ids } from '../testIds';
import { toMcpJson } from '../toMcpJson';
import { FINISHED_TIMELINE } from './stubFeatureTimeline';

// `a[].b` is key b on the first element of array a; a bare name is a top-level key, or a key of the
// row's array element (the row's first path names the array).
const resolves = (root: unknown, path: string): boolean => {
  let node: unknown = root;
  for (const part of path.split('.')) {
    const isArray = part.endsWith('[]');
    const name = isArray ? part.slice(0, -2) : part;
    if (node === null || typeof node !== 'object' || !(name in node)) return false;
    node = (node as Record<string, unknown>)[name];
    if (isArray) {
      if (!Array.isArray(node) || node.length === 0) return false;
      node = node[0];
    }
  }
  return true;
};

test('RailJsonGuide_ShouldShowTheCallTheEightRowMapAndTheRule', async ({ mount, page }) => {
  await mount(<RailJsonGuide featureKey="SHIP-99" />);
  await expect(page.getByTestId(ids.railJsonCall)).toHaveText(
    'feature_timeline({ key: "SHIP-99" })',
  );
  await expect(page.getByTestId(ids.railOverview)).toContainText(
    "Read-only. Any project member's agent. One call returns the whole picture; there is nothing to page through.",
  );
  await expect(page.getByTestId(ids.railJsonMap).locator('li')).toHaveCount(8);
  const map = page.getByTestId(ids.railJsonMap);
  for (const text of ['first Active', 'last Resolved', 'part-way', 'inferred: true'])
    await expect(map).toContainText(text);
  await expect(page.getByTestId(ids.railJsonRule)).toContainText(
    "Rule for every chart from now on: the screen and the MCP tool are built from one server view model, so a person and an agent can't see different numbers.",
  );
});

test('RailJsonGuide_EveryFieldPath_ShouldResolveInTheMcpJson', async ({ mount, page }) => {
  await mount(<RailJsonGuide featureKey="SHIP-99" />);
  const paths = await page.getByTestId(ids.railJsonMap).locator('code').allTextContents();
  expect(paths.length).toBeGreaterThan(8);
  const mcp = toMcpJson(FINISHED_TIMELINE);
  for (const path of paths) expect(resolves(mcp, path), path).toBe(true);
});
