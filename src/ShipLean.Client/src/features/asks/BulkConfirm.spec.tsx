import { expect, test } from '@playwright/experimental-ct-react';
import type { ItemAsksDto } from '@shiplean/core/api/asks';
import { BulkConfirm } from './BulkConfirm';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';
import { answered, waiting } from './ct/fixtures';
import { RegionHarness } from './ct/RegionHarness';
import { LiveBulkConfirm } from './ct/LiveBulkConfirm';
import { stubAsks } from './ct/stubAsks';

const item: AsksItem = {
  id: waiting.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
};

// Every open decision but the first is confirmed, so exactly one is open.
const oneOpen: ItemAsksDto = {
  ...waiting,
  decisions: waiting.decisions.map((decision, index) =>
    decision.state === 'open' && index > 0 ? { ...decision, state: 'confirmed' } : decision,
  ),
};

test('BulkConfirm_WhenTwoOpen_ShouldConfirmBoth', async ({ mount, page }) => {
  const stub = await stubAsks(page, waiting);
  const open = waiting.decisions.filter((decision) => decision.state === 'open');
  const component = await mount(
    <RegionHarness>
      <BulkConfirm item={item} asks={waiting} />
    </RegionHarness>,
  );
  await expect(component.getByTestId(ids.bulkConfirm)).toHaveText(
    `✓ Confirm ${open.length} recommended`,
  );
  await component.getByTestId(ids.bulkConfirm).click();
  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0]).toEqual({
    method: 'POST',
    url: `/api/work-items/${waiting.workItemId}/decisions/confirm`,
    body: { numbers: open.map((decision) => decision.number) },
  });
  await expect(page.getByText(`${open.length} decisions confirmed as recommended`)).toBeVisible();
});

test('BulkConfirm_WhenOneOpen_ShouldNotShow', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <BulkConfirm item={item} asks={oneOpen} />
    </RegionHarness>,
  );
  await expect(component.getByTestId(ids.bulkConfirm)).toHaveCount(0);
});

test('BulkConfirm_WhenConfirmLeavesFewerThanTwoOpen_ShouldStillToast', async ({ mount, page }) => {
  const stub = await stubAsks(page, waiting);
  const open = waiting.decisions.filter((decision) => decision.state === 'open');
  const component = await mount(
    <RegionHarness>
      <LiveBulkConfirm item={item} initial={waiting} />
    </RegionHarness>,
  );
  // The server's state after the write: nothing waiting, so the section folds and AsksHeader unmounts BulkConfirm.
  stub.setAsks(answered);
  await component.getByTestId(ids.bulkConfirm).click();
  await expect(component.getByTestId(ids.bulkConfirm)).toHaveCount(0);
  await expect(page.getByText(`${open.length} decisions confirmed as recommended`)).toBeVisible();
});
