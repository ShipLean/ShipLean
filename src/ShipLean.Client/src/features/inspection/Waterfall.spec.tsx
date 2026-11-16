import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { RunRowDetailDto } from '@shiplean/core/api/inspection';
import { runRowDetail, stubInspection } from './ct/inspectionMocks';
import { inspectionTestIds as ids } from './testIds';
import { WaterfallHarness } from './waterfall/ct/WaterfallHarness';

const edit: RunRowDetailDto = { ...runRowDetail, commitSha: 'a81d4e0' };

// One detail per row, matched to the row the state selects.
const failedBash: RunRowDetailDto = {
  ...runRowDetail,
  name: 'Bash',
  startLabel: '10:00:21',
  durationMs: 9000,
  offsetMs: 21000,
  waitMs: null,
  input: '{"command":"dotnet test"}',
  output: 'Failed Foo_WhenBar_ShouldBaz\nExpected 3 but was 4\nExit status 1',
  outputTruncated: true,
  filePath: null,
  commitSha: null,
};
const permissionBash: RunRowDetailDto = {
  ...failedBash,
  startLabel: '10:00:12',
  durationMs: 1_278_000,
  offsetMs: 12000,
  waitMs: 1_260_000,
  output: 'Build succeeded',
  outputTruncated: false,
};
const hiddenBash: RunRowDetailDto = {
  ...failedBash,
  contentHidden: true,
  input: null,
  output: null,
  outputTruncated: false,
};
const detailByState = {
  default: { rowId: 'err-1', detail: failedBash },
  permission: { rowId: 'perm-1', detail: permissionBash },
  edit: { rowId: '0000000000001005', detail: edit },
  hidden: { rowId: 'err-1', detail: hiddenBash },
} as const;

async function setup(page: Page, detail: RunRowDetailDto = edit, rowStatus = 200) {
  await page.setViewportSize({ width: 1440, height: 900 });
  const seen = await stubInspection(page);
  await page.route('**/api/projects/by-key/*', (route) =>
    route.fulfill({ json: { id: 'p1', key: 'SHIP', role: 'member' } }),
  );
  await page.route('**/runs/*/rows/*', (route) => {
    seen.urls.push(new URL(route.request().url()).pathname);
    return rowStatus === 200
      ? route.fulfill({ json: detail })
      : route.fulfill({
          status: rowStatus,
          json: { code: 'conflict', error: 'ClickHouse is not reachable: connection refused' },
        });
  });
  return seen;
}

for (const theme of ['light', 'dark'] as const) {
  for (const variant of ['default', 'permission', 'edit', 'hidden'] as const) {
    test(`Waterfall_WhenState${variant}_ShouldMatchItsScreenshot_${theme}`, async ({
      mount,
      page,
    }) => {
      const { rowId, detail } = detailByState[variant];
      await setup(page, detail);
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const c = await mount(<WaterfallHarness variant={variant} initial={rowId} />);
      await expect(page.getByTestId(ids.runPanelKind)).toBeVisible();
      await expect(page.locator(`[data-row-id="${rowId}"]`)).toHaveAttribute(
        'aria-selected',
        'true',
      );
      await expectPanelFor(page, variant);
      await expect(c).toHaveScreenshot(`waterfall-${variant}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}

async function expectPanelFor(page: Page, variant: keyof typeof detailByState) {
  if (variant === 'default') {
    await expect(page.getByTestId(ids.runPanelOutput)).toContainText('Expected 3 but was 4');
    await expect(page.getByTestId(ids.runPanelOutputTruncated)).toBeVisible();
    await expect(page.getByTestId(ids.runPanelFilePath)).toHaveCount(0);
    await expect(page.getByTestId(ids.runPanelWait)).toHaveCount(0);
  } else if (variant === 'permission') {
    await expect(page.getByTestId(ids.runPanelWait)).toContainText('Waited 21 m');
    await expect(page.getByTestId(ids.runPanelFilePath)).toHaveCount(0);
  } else if (variant === 'edit') {
    await expect(page.getByTestId(ids.runPanelFilePath)).toHaveText('src/App/Foo.cs');
    await expect(page.getByTestId(ids.runPanelCommit)).toHaveText('a81d4e0');
  } else {
    await expect(page.getByTestId(ids.runPanelContentHidden)).toBeVisible();
    await expect(page.getByTestId(ids.runPanelOutput)).toHaveCount(0);
    await expect(page.getByTestId(ids.runPanelFilePath)).toHaveCount(0);
  }
}

test('Waterfall_WhenRendered_ShouldShowKindsWaitErrorRetryAndCollapsedCount', async ({
  mount,
  page,
}) => {
  await setup(page);
  await mount(<WaterfallHarness variant="default" />);
  await expect(page.getByTestId(ids.runTurn)).toHaveCount(2);
  await expect(page.getByTestId(ids.runRow)).toHaveCount(5);
  await expect(page.getByTestId(ids.runRowWait)).toHaveCount(1);
  await expect(page.getByTestId(ids.runRowError)).toHaveCount(1);
  await expect(page.getByTestId(ids.runRowRetry)).toHaveCount(1);
  await expect(page.getByTestId(ids.runRowCollapsed)).toHaveText('2 spans');
});

test('Waterfall_WhenContentHidden_ShouldSayPromptHidden', async ({ mount, page }) => {
  await setup(page);
  await mount(<WaterfallHarness variant="hidden" />);
  await expect(page.getByTestId(ids.runTurnPromptHidden)).toHaveCount(2);
  await expect(page.getByTestId(ids.runTurnPrompt)).toHaveCount(0);
});

test('Waterfall_WhenRowSelectedAndRerendered_ShouldFetchItsDetailOnce', async ({ mount, page }) => {
  const seen = await setup(page);
  await mount(<WaterfallHarness variant="default" />);
  await page.locator('[data-row-id="0000000000001005"]').click();
  await expect(page.getByTestId(ids.runPanelFilePath)).toHaveText('src/App/Foo.cs');
  // Interacting with the panel must not fetch the detail again.
  await page.getByTestId(ids.runPanelCopyLink).click();
  await page.locator('[data-row-id="0000000000001005"]').hover();
  expect(seen.urls.filter((u) => /\/rows\/0000000000001005$/.test(u))).toHaveLength(1);
});

test('Waterfall_WhenRowSelected_ShouldPostRowSelectedUsageOnce', async ({ mount, page }) => {
  const seen = await setup(page);
  const posts: string[] = [];
  page.on('request', (r) => {
    if (r.method() === 'POST' && r.url().endsWith('/usage/inspection'))
      posts.push(r.postData() ?? '');
  });
  await mount(<WaterfallHarness variant="default" />);
  await page.locator('[data-row-id="0000000000001005"]').click();
  await expect(page.getByTestId(ids.runPanelKind)).toBeVisible();
  await expect.poll(() => posts.length).toBe(1);
  expect(JSON.parse(posts[0])).toEqual({ action: 'rowSelected', itemKey: 'SHIP-1' });
  expect(seen.urls.length).toBeGreaterThan(0);
});

test('Waterfall_WhenFourHundredRows_ShouldRenderAllOfThem', async ({ mount, page }) => {
  await setup(page);
  await mount(<WaterfallHarness variant="big" />);
  await expect(page.getByTestId(ids.runRow)).toHaveCount(400);
});

test('RowPanel_WhenStoreIsUnreachable_ShouldShowTheProblemText', async ({ mount, page }) => {
  await setup(page, edit, 409);
  await mount(<WaterfallHarness variant="default" initial="0000000000001005" />);
  const failed = page.getByTestId(ids.runPanelLoadFailed);
  await expect(failed).toHaveAttribute('data-reason', 'store-unreachable');
  await expect(failed).toContainText('ClickHouse is not reachable: connection refused');
});

test('RowPanel_WhenDetailFails500_ShouldShowGenericFailureNotStoreUnreachable', async ({
  mount,
  page,
}) => {
  await setup(page, edit, 500);
  await mount(<WaterfallHarness variant="default" initial="0000000000001005" />);
  const failed = page.getByTestId(ids.runPanelLoadFailed);
  await expect(failed).toBeVisible();
  await expect(failed).not.toHaveAttribute('data-reason', 'store-unreachable');
});

test('RowPanel_WhenContentHidden_ShouldShowTheNoteInsteadOfInputAndOutput', async ({
  mount,
  page,
}) => {
  await setup(page, { ...edit, contentHidden: true, input: null, output: null });
  await mount(<WaterfallHarness variant="hidden" initial="0000000000001005" />);
  await expect(page.getByTestId(ids.runPanelContentHidden)).toBeVisible();
  await expect(page.getByTestId(ids.runPanelInput)).toHaveCount(0);
  await expect(page.getByTestId(ids.runPanelOutput)).toHaveCount(0);
  await expect(page.getByTestId(ids.runPanelAttribute)).toHaveCount(2);
  await expect(page.getByTestId(ids.runPanelLog)).toHaveCount(1);
});

test('RowPanel_WhenALogSeverityIsUnspecified_ShouldShowNoSeverityPillForIt', async ({
  mount,
  page,
}) => {
  const [info] = edit.logs;
  await setup(page, { ...edit, logs: [info, { ...info, severity: 'UNSPECIFIED' }] });
  await mount(<WaterfallHarness variant="default" initial="0000000000001005" />);
  const logs = page.getByTestId(ids.runPanelLog);
  await expect(logs).toHaveCount(2);
  await expect(logs.nth(0).locator('.rp-sv')).toHaveText('INFO');
  await expect(logs.nth(1).locator('.rp-sv')).toHaveCount(0);
  await expect(logs.nth(1)).toContainText('tool_result');
});
