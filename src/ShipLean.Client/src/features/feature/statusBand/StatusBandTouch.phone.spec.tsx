import { expect, test } from '@playwright/experimental-ct-react';
import type { StateDto } from '@shiplean/core/api/projects';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';

// The Feature's status band, editor, fields and Stories and bugs on the CT phone project (390×844, SHIP-714).

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

const states: StateDto[] = [
  { id: 's-idea', type: 'feature', name: 'Idea', category: 'proposed', order: 1, itemCount: 0 },
  {
    id: 's-active',
    type: 'feature',
    name: 'Active',
    category: 'inProgress',
    order: 2,
    itemCount: 1,
  },
];

const longLine =
  'Waiting on the card vault rate limit before the saved-card flow can be switched on for everyone.';

const child = (key: string, title: string) => ({
  ...feature.children[0],
  id: `c-${key}`,
  key,
  title,
});

async function stubAll(page: StubbedPage, item = feature) {
  await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  await page.route('**/api/projects/p1/states', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(states) }),
  );
}

const boxOf = async (locator: {
  boundingBox(): Promise<{ x: number; y: number; width: number; height: number } | null>;
}) => (await locator.boundingBox())!;

test('StatusBand_WhenTouch_ShouldStackChipLineAndButtons', async ({ mount, page }) => {
  await stubAll(page, { ...feature, statusLine: longLine });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const band = await boxOf(screen.getByTestId(ids.statusBand));
  const chip = await boxOf(screen.getByTestId(ids.statusState));
  const line = await boxOf(screen.getByTestId(ids.statusLine));
  const verified = await boxOf(screen.getByTestId(ids.statusVerified));
  const still = await boxOf(screen.getByTestId(ids.statusStillTrue));
  const edit = await boxOf(screen.getByTestId(ids.statusEdit));

  // Chip, then the line (full width, wrapped), then Verified, then the two buttons side by side.
  expect(chip.width).toBeLessThan(band.width / 2);
  expect(line.y).toBeGreaterThanOrEqual(chip.y + chip.height - 1);
  expect(line.width).toBeGreaterThan(band.width - 40);
  expect(line.height).toBeGreaterThan(40);
  expect(verified.y).toBeGreaterThanOrEqual(line.y + line.height - 1);
  expect(still.y).toBeGreaterThanOrEqual(verified.y + verified.height - 1);
  expect(edit.y).toBe(still.y);
  expect(edit.x).toBeGreaterThan(still.x + still.width - 2);
  expect(still.height).toBeGreaterThanOrEqual(32);
  expect(edit.height).toBeGreaterThanOrEqual(32);
});

test('StatusEditor_WhenTouch_ShouldSaySaveWritesIt', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.statusEdit).tap();

  const hint = screen.getByTestId(ids.statusEditHint);
  await expect(hint).toBeVisible();
  await expect(hint).toContainText('Save writes it with any other changes.');
  await expect(hint).not.toContainText('in the header');
  const editor = await boxOf(screen.getByTestId(ids.statusEditor));
  const state = await boxOf(screen.getByTestId(ids.statusEditState));
  const area = await boxOf(screen.getByTestId(ids.statusEditLine));
  // One column: the state picker over the line, both the editor's full width.
  expect(area.y).toBeGreaterThan(state.y + state.height - 1);
  expect(area.width).toBeGreaterThan(editor.width - 2);
  expect(
    await screen.getByTestId(ids.statusEditLine).evaluate((el) => getComputedStyle(el).fontSize),
  ).toBe('16px');
  for (const id of [ids.statusEditSave, ids.statusEditCancel]) {
    expect((await boxOf(screen.getByTestId(id))).height).toBeGreaterThanOrEqual(32);
  }
});

test('StoriesAndBugs_WhenTouch_ShouldStackRows', async ({ mount, page }) => {
  const title = 'Pay with a saved card from the review step without re-entering the security code';
  await stubAll(page, {
    ...feature,
    children: [child('SHIP-141', title), child('SHIP-142', 'Short')],
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const row = screen.getByTestId(ids.childRow).first();
  const rowBox = await boxOf(row);
  const titleBox = await boxOf(row.getByTestId(ids.childTitle));
  const keyBox = await boxOf(row.getByTestId(ids.childKey));
  const stateBox = await boxOf(row.getByTestId(ids.childState));

  // The title (wrapped, not truncated) on top; key, state and person on the line below it.
  expect(titleBox.height).toBeGreaterThan(32);
  expect(titleBox.x + titleBox.width).toBeLessThanOrEqual(rowBox.x + rowBox.width + 1);
  expect(keyBox.y).toBeGreaterThanOrEqual(titleBox.y + titleBox.height - 1);
  expect(stateBox.y).toBe(keyBox.y);
  expect(stateBox.x).toBeGreaterThan(keyBox.x + keyBox.width - 1);
  expect(titleBox.height).toBeGreaterThanOrEqual(32);
  // Even a short title keeps its own line.
  const short = screen.getByTestId(ids.childRow).nth(1);
  const shortTitle = await boxOf(short.getByTestId(ids.childTitle));
  const shortKey = await boxOf(short.getByTestId(ids.childKey));
  expect(shortKey.y).toBeGreaterThanOrEqual(shortTitle.y + shortTitle.height - 1);
  expect(shortTitle.height).toBeGreaterThanOrEqual(32);
});

test('FeatureFieldsStrip_WhenTouch_ShouldLayOutTwoColumnTiles', async ({ mount, page }) => {
  await stubAll(page, { ...feature, slug: 'checkout-v2', startedLabel: 'Mon 21 Sep' });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const strip = screen.getByTestId(ids.strip);
  const tile = (field: string) => strip.locator(`[data-field="${field}"]`);
  const strip_ = await boxOf(strip);
  const state = await boxOf(tile('state'));
  const epic = await boxOf(tile('parent'));
  const slug = await boxOf(tile('slug'));
  const started = await boxOf(tile('started'));
  const children = await boxOf(tile('children'));
  const tags = await boxOf(tile('tags'));

  // State | Epic, Slug | Started, then Stories and bugs and Tags each on a whole row.
  expect(epic.y).toBe(state.y);
  expect(epic.x).toBeGreaterThan(state.x + state.width - 2);
  expect(state.width).toBeGreaterThan(strip_.width / 2 - 4);
  expect(slug.y).toBeGreaterThan(state.y);
  expect(started.y).toBe(slug.y);
  expect(started.x).toBeGreaterThan(slug.x + slug.width - 2);
  let above = slug.y;
  for (const box of [children, tags]) {
    expect(box.width).toBeGreaterThan(strip_.width - 2);
    expect(box.y).toBeGreaterThan(above);
    above = box.y;
  }
  expect((await boxOf(screen.getByTestId(ids.stripEpic))).height).toBeGreaterThanOrEqual(32);
});
