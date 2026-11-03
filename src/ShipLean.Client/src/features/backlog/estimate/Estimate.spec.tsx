import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../components/testIds';
import { ToastHost } from '../../../components/ToastHost';
import { backlogView, PROJECT } from '../ct/fixtures';
import { ListMode } from '../list/ListMode';
import { EstimateHarness } from '../ct/EstimateHarness';
import { ESTIMATE_ROWS } from '../ct/estimateRows';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };

test('Click_opens_the_field_on_that_row', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness />, route);

  await page.getByTestId(ids.row).nth(1).getByTestId(ids.points).click();

  const input = page.getByTestId(ids.pointsInput);
  await expect(input).toHaveCount(1);
  await expect(input).toBeFocused();
  await expect(page.getByTestId(ids.row).nth(1).getByTestId(ids.pointsInput)).toBeVisible();
});

test('Estimate_command_with_a_digit_starts_the_field_with_it', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness />, route);
  await page.getByTestId(ids.row).nth(2).focus();

  await page.getByTestId('command-arg').fill('5');
  await page.getByTestId('run-command-estimate').click();

  const input = page.getByTestId(ids.pointsInput);
  await expect(page.getByTestId(ids.row).nth(2).getByTestId(ids.pointsInput)).toHaveValue('5');
  await expect(input).toBeFocused();
});

test('Enter_saves_and_moves_down', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness />, route);
  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();

  await page.getByTestId(ids.pointsInput).fill('5');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].url).toContain('/api/work-items/id-SHIP-1');
  expect(server.patches[0].body).toMatchObject({
    expectedRevision: 4,
    changes: { points: { value: 5 } },
  });
  await expect(page.getByTestId(ids.row).nth(1).getByTestId(ids.pointsInput)).toBeFocused();
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
});

test('Enter_on_the_last_row_saves_and_stays', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness />, route);
  await page.getByTestId(ids.row).nth(2).getByTestId(ids.points).click();

  await page.getByTestId(ids.pointsInput).fill('13');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect.poll(() => server.patches.length).toBe(1);
  await expect(page.getByTestId(ids.pointsInput)).toHaveCount(0);
});

test('Esc_cancels_and_sends_nothing', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness />, route);
  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();

  await page.getByTestId(ids.pointsInput).fill('21');
  await page.getByTestId(ids.pointsInput).press('Escape');

  await expect(page.getByTestId(ids.pointsInput)).toHaveCount(0);
  await expect(page.getByTestId(ids.row).nth(0).getByTestId(ids.points)).toHaveText('3');
  expect(server.patches).toHaveLength(0);
});

test('Empty_clears_the_estimate', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness />, route);
  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();

  await page.getByTestId(ids.pointsInput).fill('');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].body).toMatchObject({ changes: { points: { value: null } } });
});

test('Invalid_value_is_not_sent', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness />, route);
  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();

  await page.getByTestId(ids.pointsInput).fill('abc');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect(page.getByTestId(ids.pointsInput)).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByTestId(ids.pointsInput)).toHaveValue('abc');
  expect(server.patches).toHaveLength(0);
});

test('Stale_answer_keeps_the_value_and_offers_use_mine', async ({ mount, page }) => {
  const current = { ...ESTIMATE_ROWS[0], points: 13, revision: 9 };
  const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS), {
    patchAnswers: [
      {
        status: 409,
        body: {
          error: 'This item changed.',
          code: 'conflict',
          current,
          theirChanges: [{ rev: 9, changedBy: { name: 'Tom Okafor' }, changes: [] }],
        },
      },
      {
        status: 200,
        body: { id: 'id-SHIP-1', key: 'SHIP-1', revision: 10, changes: [], notices: [] },
      },
    ],
  });
  await mount(
    <>
      <ToastHost />
      <EstimateHarness />
    </>,
    route,
  );
  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();
  await page.getByTestId(ids.pointsInput).fill('5');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toContainText('Tom Okafor');
  await expect(page.getByTestId(ids.row).nth(0).getByTestId(ids.points)).toHaveText('5');

  await page.getByTestId(componentTestIds.conflictUseMine).click();

  await expect.poll(() => server.patches.length).toBe(2);
  expect(server.patches[1].body).toMatchObject({
    expectedRevision: 9,
    changes: { points: { value: 5 } },
  });
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
});

test('Edit_made_while_a_save_is_pending_goes_out_after_it_at_the_new_revision', async ({
  mount,
  page,
}) => {
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS), {
    holdFirstPatch: held,
    patchAnswers: [
      {
        status: 200,
        body: { id: 'id-SHIP-1', key: 'SHIP-1', revision: 5, changes: [], notices: [] },
      },
      {
        status: 200,
        body: { id: 'id-SHIP-1', key: 'SHIP-1', revision: 6, changes: [], notices: [] },
      },
    ],
  });
  await mount(<ListMode project={PROJECT} />, route);
  const first = page.getByTestId(ids.row).nth(0);
  await expect(first.getByTestId(ids.points)).toHaveText('3');

  await first.getByTestId(ids.points).click();
  await page.getByTestId(ids.pointsInput).fill('5');
  await page.getByTestId(ids.pointsInput).press('Enter');
  await expect.poll(() => server.patches.length).toBe(1);

  // The first save is still pending and the list still shows 3. Typing 3 again is a real change from the 5 being saved.
  await first.getByTestId(ids.points).click();
  await page.getByTestId(ids.pointsInput).fill('3');
  await page.getByTestId(ids.pointsInput).press('Enter');
  expect(server.patches).toHaveLength(1);
  await expect(first.getByTestId(ids.points)).toHaveText('3');

  server.setBacklog(
    backlogView([{ ...ESTIMATE_ROWS[0], points: 5, revision: 5 }, ...ESTIMATE_ROWS.slice(1)]),
  );
  release();

  await expect.poll(() => server.patches.length).toBe(2);
  expect(server.patches[1].body).toMatchObject({
    expectedRevision: 5,
    changes: { points: { value: 3 } },
  });
});

for (const ending of ['the_backlog_read_fails', 'the_row_leaves_the_screen'] as const) {
  test(`Queued_edit_is_sent_when_${ending}`, async ({ mount, page }) => {
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    let reads = 'ok';
    const server = await stubBacklog(page, backlogView(ESTIMATE_ROWS), {
      holdFirstPatch: held,
      answer: () => (reads === 'fail' ? { status: 500, body: { error: 'down' } } : undefined),
      patchAnswers: [
        {
          status: 200,
          body: { id: 'id-SHIP-1', key: 'SHIP-1', revision: 5, changes: [], notices: [] },
        },
        {
          status: 200,
          body: { id: 'id-SHIP-1', key: 'SHIP-1', revision: 6, changes: [], notices: [] },
        },
      ],
    });
    const component = await mount(<ListMode project={PROJECT} />, route);
    const first = page.getByTestId(ids.row).nth(0);
    await first.getByTestId(ids.points).click();
    await page.getByTestId(ids.pointsInput).fill('5');
    await page.getByTestId(ids.pointsInput).press('Enter');
    await expect.poll(() => server.patches.length).toBe(1);
    await first.getByTestId(ids.points).click();
    await page.getByTestId(ids.pointsInput).fill('3');
    await page.getByTestId(ids.pointsInput).press('Enter');

    // The list still holds 3 at revision 4 and never reads the 5 back; the revert to 3 must still go out.
    reads = 'fail';
    if (ending === 'the_row_leaves_the_screen') await component.unmount();
    release();

    await expect.poll(() => server.patches.length).toBe(2);
    expect(server.patches[1].body).toMatchObject({
      expectedRevision: 5,
      changes: { points: { value: 3 } },
    });
  });
}

test('Tree_mode_points_are_read_only', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ESTIMATE_ROWS));
  await mount(<EstimateHarness tree />, route);

  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();

  await expect(page.getByTestId(ids.pointsInput)).toHaveCount(0);
  await expect(page.getByTestId(ids.row).nth(0).getByTestId(ids.points)).toHaveText('3');
  await expect(page.getByTestId(ids.row).nth(1).getByTestId(ids.points)).toHaveText('?');
});

for (const width of [1280, 1440]) {
  test(`List_at_${width}_with_a_conflict_does_not_overflow`, async ({ mount, page }) => {
    await page.setViewportSize({ width, height: 800 });
    await stubBacklog(page, backlogView(ESTIMATE_ROWS), {
      patchAnswers: [
        {
          status: 409,
          body: {
            error: 'This item changed.',
            code: 'conflict',
            current: { ...ESTIMATE_ROWS[0], revision: 9 },
            theirChanges: [],
          },
        },
      ],
    });
    await mount(<ListMode project={PROJECT} />, route);
    await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();
    await page.getByTestId(ids.pointsInput).fill('5');
    await page.getByTestId(ids.pointsInput).press('Enter');

    const banner = page.getByTestId(componentTestIds.conflictBanner);
    await expect(banner).toBeVisible();
    const box = (await banner.boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(width);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
      width,
    );
  });
}
