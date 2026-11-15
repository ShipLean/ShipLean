import { expect, test } from '@playwright/experimental-ct-react';
import type { LinkDto } from '@shiplean/core/api/workItems';
import { mei, story } from '../ct/fixtures';
import { SideRailHarness } from '../ct/SideRailHarness';
import { stubApi } from '../ct/stubApi';
import { attachment, change, revision, stubSide } from '../ct/stubSide';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

// The Links and Attachments sections on the CT phone project (390×844, SHIP-714 D6/D7, SHIP-805 rules 4, 5, 13).

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const related: LinkDto = {
  id: 'l1',
  type: 'related',
  workItemId: 'w144',
  key: 'SHIP-144',
  title: 'Saved cards list',
  url: null,
  card: null,
  autoLinked: null,
};

test('LinkRow_WhenTouch_ShouldShowRemoveWithoutHover', async ({ mount, page }) => {
  await stubApi(page, { ...story, links: [related] });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);
  const row = screen.getByTestId(ids.linkRow);
  await expect(row).toHaveCount(1);

  // Nothing here has been hovered or focused; the remove and edit buttons are still painted and 32px.
  const remove = row.getByTestId(ids.linkRemove);
  await expect(remove).toBeVisible();
  const box = (await remove.boundingBox())!;
  expect(box.width).toBeGreaterThanOrEqual(32);
  expect(box.height).toBeGreaterThanOrEqual(32);
  const edit = (await row.getByTestId(ids.linkEdit).boundingBox())!;
  expect(edit.height).toBeGreaterThanOrEqual(32);
  expect(await remove.evaluate((el) => getComputedStyle(el).opacity)).toBe('1');
  const rowBox = (await row.boundingBox())!;
  expect(box.x + box.width).toBeLessThanOrEqual(rowBox.x + rowBox.width + 0.5);

  // One tap removes it (no hover step needed).
  await remove.tap();
  await expect(screen.getByTestId(ids.linkRow)).toHaveCount(0);
});

test('LinkForm_WhenTouch_ShouldFillTheWidthWithTouchSizedControls', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);
  await screen.getByTestId(ids.linkAdd).tap();
  const form = screen.getByTestId(ids.linkForm);
  const formBox = (await form.boundingBox())!;
  const key = screen.getByTestId(ids.linkKey);
  const keyBox = (await key.boundingBox())!;
  expect(keyBox.width).toBeGreaterThan(formBox.width - 24);
  expect(keyBox.height).toBeGreaterThanOrEqual(32);
  expect(await key.evaluate((el) => getComputedStyle(el).fontSize)).toBe('16px');
  expect((await screen.getByTestId(ids.linkSubmit).boundingBox())!.height).toBeGreaterThanOrEqual(
    32,
  );
});

test('AttachmentsSection_WhenTouch_ShouldUseFilePickerWithoutDropOverlay', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubSide(page, mei, [attachment('a1', 'saved-card-picker.png', mei)], []);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);
  const section = screen.getByTestId(ids.attachments);
  await expect(section.getByTestId(ids.attachment)).toHaveCount(1);

  // + Attach opens the device's own picker; it is not narrowed to a type or forced to the camera.
  const input = section.getByTestId(ids.attachmentFile);
  expect(await input.getAttribute('accept')).toBeNull();
  expect(await input.getAttribute('capture')).toBeNull();
  const attach = section.getByTestId(ids.attachmentUpload);
  expect((await attach.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  const chooser = page.waitForEvent('filechooser');
  await attach.tap();
  expect((await chooser).isMultiple()).toBe(true);

  // Remove is always there and 32px; a drag over the section paints no overlay.
  const remove = section.getByTestId(ids.attachmentArchive);
  expect((await remove.boundingBox())!.width).toBeGreaterThanOrEqual(32);
  const before = await section.evaluate((el) => el.querySelectorAll('*').length);
  await section.evaluate((el) => {
    const data = new DataTransfer();
    data.items.add(new File(['x'], 'x.txt'));
    el.dispatchEvent(new DragEvent('dragenter', { dataTransfer: data, bubbles: true }));
    el.dispatchEvent(new DragEvent('dragover', { dataTransfer: data, bubbles: true }));
  });
  expect(await section.evaluate((el) => el.querySelectorAll('*').length)).toBe(before);
  await expect(page.getByText(/drop/i)).toHaveCount(0);
});

test('HistorySection_WhenFourRevisionsOnTouch_ShouldHideTheOldestUntilShowAll', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  const revisions = [4, 3, 2, 1].map((rev) => revision(rev, mei, [change('points', rev, rev + 1)]));
  await stubSide(page, mei, [], revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.revision)).toHaveCount(3);
  await expect(screen.getByTestId(ids.historyShowAll)).toHaveText('Show all 4 revisions');
  await screen.getByTestId(ids.historyShowAll).tap();
  await expect(screen.getByTestId(ids.revision)).toHaveCount(4);
  await expect(screen.getByTestId(ids.historyShowAll)).toHaveCount(0);
});
