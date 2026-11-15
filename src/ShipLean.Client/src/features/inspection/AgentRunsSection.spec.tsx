import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { AgentRunsSectionDto } from '@shiplean/core/api/inspection';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { agentRunsSection, stubInspection } from './ct/inspectionMocks';
import { AgentRunsSection } from './AgentRunsSection';
import { inspectionTestIds as ids } from './testIds';

const project = { id: 'p1', key: 'SHIP', role: 'member' } as ProjectDto;
const usageCalls = (urls: string[]) => urls.filter((u) => u.endsWith('/usage/inspection'));

const empty: AgentRunsSectionDto = {
  ...agentRunsSection,
  rows: [],
  totals: { runs: 0, tookMs: 0, tokensIn: null, tokensOut: null, errors: 0, waitMs: 0 },
};
const down: AgentRunsSectionDto = {
  ...empty,
  storeReachable: false,
  problem: 'Tempo at tempo.internal did not answer within 10 s.',
};

async function open(
  page: Page,
  mount: (c: React.JSX.Element) => Promise<unknown>,
  section?: AgentRunsSectionDto,
) {
  await page.setViewportSize({ width: 1440, height: 900 });
  const seen = await stubInspection(page, { section });
  await mount(<AgentRunsSection project={project} itemKey="SHIP-1" />);
  return seen;
}

test('Section_WithRuns_ShouldShowTotalsRowsAndFooter', async ({ mount, page }) => {
  await open(page, mount);

  await expect(page.getByTestId(ids.agentRunsRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.agentRunsTotalRuns)).toHaveText('2 runs');
  await expect(page.getByTestId(ids.agentRunsTotalTook)).toHaveText('1 m');
  await expect(page.getByTestId(ids.agentRunsTotalTokens)).toHaveText('8.5k tokens');
  await expect(page.getByTestId(ids.agentRunsTotalErrors)).toHaveText('4 errors');
  await expect(page.getByTestId(ids.agentRunsTotalWait)).toContainText('Waited 3 s');
  const first = page.getByTestId(ids.agentRunsRow).first();
  await expect(first.getByTestId(ids.agentRunsRowAgent)).toHaveText('Claude Code');
  await expect(first.getByTestId(ids.agentRunsRowWhatItDid)).toHaveText(
    'report_task: done · 1 commit',
  );
  await expect(first.getByTestId(ids.agentRunsRowLinkSource)).toHaveText('Tagged');
  await expect(first.getByTestId(ids.agentRunsRowMeta)).toContainText('2 turns · 23 spans');
  await expect(first.getByTestId(ids.agentRunsRowTook)).toHaveText('1 m 25 s');
  await expect(first.getByTestId(ids.agentRunsRowTimeline)).toHaveJSProperty(
    'childElementCount',
    4,
  );
  await expect(page.getByTestId(ids.agentRunsStore)).toContainText('ClickHouse · kept 30 days');
  await expect(page.getByTestId(ids.agentRunsContentNote)).toContainText('hidden');
  await expect(page.getByTestId(ids.agentRunsSetupLink)).toHaveAttribute(
    'href',
    '/p/SHIP/settings/inspection',
  );
});

test('Section_LogOnlyRowWithoutTokens_ShouldShowDashesAndNoTrace', async ({ mount, page }) => {
  await open(page, mount);
  const cursor = page.getByTestId(ids.agentRunsRow).nth(1);

  await expect(cursor.getByTestId(ids.agentRunsRowWhatItDid)).toHaveText('—');
  await expect(cursor.getByTestId(ids.agentRunsRowTokens)).toHaveText('—');
  await expect(cursor.getByTestId(ids.agentRunsRowMeta)).toContainText('6 events, no trace');
  await expect(cursor.getByTestId(ids.agentRunsRowLinkSource)).toHaveText('.shiplean file');
  await expect(cursor.getByTestId(ids.agentRunsRowErrors)).toHaveText('0');
});

test('Section_RowAgentLink_ShouldPointAtTheRunPage', async ({ mount, page }) => {
  await open(page, mount);

  await expect(page.getByTestId(ids.agentRunsRowAgent).first()).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-1/runs/sess-cc-1',
  );
});

test('Section_WithRuns_ShouldPostSectionSeenOnce', async ({ mount, page }) => {
  const seen = await open(page, mount);

  await expect.poll(() => usageCalls(seen.urls).length).toBe(1);
  expect(usageCalls(seen.urls)).toHaveLength(1);
});

test('Section_WithNoRuns_ShouldShowSetupLinksAndNotPostSectionSeen', async ({ mount, page }) => {
  const seen = await open(page, mount, empty);

  await expect(page.getByTestId(ids.agentRunsEmpty)).toContainText('No agent runs for SHIP-1');
  await expect(page.getByTestId(ids.agentRunsEmptySetupLink)).toHaveCount(3);
  await expect(page.getByTestId(ids.agentRunsEmptySetupLink).nth(1)).toHaveAttribute(
    'href',
    '/p/SHIP/settings/inspection?agent=codex-cli',
  );
  await expect(page.getByTestId(ids.agentRunsRow)).toHaveCount(0);
  expect(usageCalls(seen.urls)).toHaveLength(0);
});

test('Section_WhenStoreUnreachable_ShouldShowProblemInsideTheSectionOnly', async ({
  mount,
  page,
}) => {
  const seen = await open(page, mount, down);

  await expect(page.getByTestId(ids.agentRunsDown)).toContainText(
    'Tempo at tempo.internal did not answer within 10 s.',
  );
  await expect(page.getByTestId(ids.agentRunsEmpty)).toHaveCount(0);
  await expect(page.getByTestId(ids.agentRunsRow)).toHaveCount(0);
  expect(usageCalls(seen.urls)).toHaveLength(0);
});

test('Section_WhenLoadFails_ShouldShowTheGenericFailure', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.route('**/api/inspection/**', (route) =>
    route.fulfill({ status: 500, json: { error: 'boom' } }),
  );
  await mount(<AgentRunsSection project={project} itemKey="SHIP-1" />);

  await expect(page.getByTestId(ids.agentRunsLoadFailed)).toBeVisible();
  await expect(page.getByTestId(ids.agentRunsDown)).toHaveCount(0);
});

const shots = { default: undefined, empty, down } as const;
for (const theme of ['light', 'dark'] as const) {
  for (const [state, section] of Object.entries(shots)) {
    test(`Section_WhenState${state}_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await open(page, mount, section);
      const target = page.getByTestId(ids.agentRunsSection);
      // Each state's own data, so the baseline cannot pass on the wrong state.
      const marker = {
        default: ids.agentRunsRow,
        empty: ids.agentRunsEmpty,
        down: ids.agentRunsDown,
      }[state as keyof typeof shots];
      await expect(page.getByTestId(marker).first()).toBeVisible();
      await expect(target).toHaveScreenshot(`agent-runs-section-${state}.${theme}.png`);
    });
  }
}
