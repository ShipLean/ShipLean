import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { attachment, stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { touchTestIds } from '../../../touch/testIds';
import { feature, linkCards, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';

// The Feature's links, Add link form, drop zone and attachment rows on the CT phone project (390×844, SHIP-714, L714f).

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

const attached = [
  { ...attachment('m1', 'design-v0.2.md', priya, 14336), kind: 'markdown' as const },
  { ...attachment('f1', 'funnel-oct.csv', priya, 31744), kind: 'file' as const },
];

// The fixture without the card that is due for a refresh.
const settled = { ...linkCards, pages: linkCards.pages.filter((card) => !card.refreshDue) };

async function stubAll(page: StubbedPage) {
  await stubApi(page, feature);
  await stubSide(page, priya, attached, []);
  await stubComments(page, priya, []);
  return stubFeature(page, { cards: settled });
}

test('LinkCard_WhenMoreTapped_ShouldOpenActionSheet', async ({ mount, page }) => {
  const stub = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const card = screen.getByTestId(ids.linkCard).filter({ hasText: 'Rotate vault keys' });

  // One column: the cards sit under each other.
  await expect(screen.getByTestId(ids.linkGroup).first().getByTestId(ids.linkCard)).toHaveCount(3);
  const boxes = await screen
    .getByTestId(ids.linkGroup)
    .first()
    .getByTestId(ids.linkCard)
    .evaluateAll((nodes) => nodes.slice(0, 2).map((node) => node.getBoundingClientRect()));
  expect(boxes[1]!.top).toBeGreaterThan(boxes[0]!.bottom - 1);
  expect(boxes[0]!.width).toBeGreaterThan(300);
  // The state chip is on the title line and the ✕ is in view without opening anything.
  const title = await card.getByTestId(ids.linkCardTitle).boundingBox();
  const chip = await card.getByTestId(ids.linkCardState).boundingBox();
  expect(chip!.y).toBeLessThan(title!.y + title!.height);
  await expect(card.getByTestId(ids.linkCardClose)).toBeVisible();

  await card.getByRole('button', { name: 'Link actions' }).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(2);
  await page.locator(`[data-action-id="remove"]`).tap();

  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(card).toHaveCount(0);
  expect(stub.calls.some((c) => c.call === 'archiveLinkCard')).toBe(true);
});

test('LinkCard_WhenCornerCrossTapped_ShouldRemoveIt', async ({ mount, page }) => {
  const stub = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const card = screen.getByTestId(ids.linkCard).filter({ hasText: 'Rotate vault keys' });
  const cross = await card.getByTestId(ids.linkCardClose).boundingBox();
  expect(cross!.width).toBeGreaterThanOrEqual(32);
  expect(cross!.height).toBeGreaterThanOrEqual(32);

  await card.getByTestId(ids.linkCardClose).tap();

  await expect(card).toHaveCount(0);
  expect(stub.calls.some((c) => c.call === 'archiveLinkCard')).toBe(true);
});

test('AddLinkForm_WhenTouch_ShouldStackFields', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const root = screen.getByTestId(ids.addLink);
  await root.getByTestId(ids.addLinkOpen).tap();

  const form = (await root.getByTestId(ids.addLinkForm).boundingBox())!;
  const title = (await root.getByTestId(ids.addLinkTitle).boundingBox())!;
  const note = (await root.getByTestId(ids.addLinkNote).boundingBox())!;
  expect(note.y).toBeGreaterThanOrEqual(title.y + title.height - 1);
  expect(title.width).toBeGreaterThan(form.width - 40);
  expect(note.width).toBeGreaterThan(form.width - 40);

  await root.getByTestId(ids.addLinkTabWorkItem).tap();
  const key = (await root.getByTestId(ids.addLinkKey).boundingBox())!;
  const type = (await root.getByTestId(ids.addLinkType).boundingBox())!;
  expect(type.y).toBeGreaterThanOrEqual(key.y + key.height - 1);
  expect(key.width).toBeGreaterThan(form.width - 40);
  for (const id of [ids.addLinkSubmit, ids.addLinkCancel, ids.addLinkTabUrl]) {
    expect((await root.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
});

test('FeatureDropZone_WhenTouch_ShouldNotShowOverlay', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const zone = screen.getByTestId(ids.dropZone);
  await expect(zone).toBeVisible();

  await zone.dispatchEvent('dragover', {
    dataTransfer: await page.evaluateHandle(() => {
      const transfer = new DataTransfer();
      transfer.items.add(new File(['x'], 'x.txt'));
      return transfer;
    }),
  });

  await expect(zone).not.toHaveClass(/outline-dashed/);
  await expect(zone).toHaveCSS('outline-style', 'none');
});

test('RailItem_WhenTouch_ShouldShowRemoveWithoutHover', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const rail = screen.getByTestId(ids.attachmentsRail);
  const items = rail.getByTestId(ids.railItem);
  await expect(items).toHaveCount(2);

  for (let i = 0; i < 2; i++) {
    const row = (await items.nth(i).boundingBox())!;
    const remove = rail.getByTestId(ids.railItemRemove).nth(i);
    await expect(remove).toBeVisible();
    const cross = (await remove.boundingBox())!;
    expect(row.height).toBeGreaterThanOrEqual(32);
    expect(cross.height).toBeGreaterThanOrEqual(32);
    expect(cross.width).toBeGreaterThanOrEqual(32);
  }
});
