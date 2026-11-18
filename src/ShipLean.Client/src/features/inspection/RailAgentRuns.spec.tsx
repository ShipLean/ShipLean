import { expect, test } from '@playwright/experimental-ct-react';
import type { AgentRunsSectionDto } from '@shiplean/core/api/inspection';
import { agentRunsSection, stubInspection } from './ct/inspectionMocks';
import { RailAgentRuns } from './RailAgentRuns';
import { inspectionTestIds as ids } from './testIds';

const empty: AgentRunsSectionDto = {
  ...agentRunsSection,
  rows: [],
  totals: { runs: 0, tookMs: 0, tokensIn: null, tokensOut: null, errors: 0, waitMs: 0 },
};

test('RailAgentRuns_WithRuns_ShouldLinkEachRunToItsRunPage', async ({ mount, page }) => {
  const seen = await stubInspection(page);
  await mount(<RailAgentRuns project="SHIP" itemKey="SHIP-1" />);

  const rows = page.getByTestId(ids.railAgentRunsRow);
  await expect(rows).toHaveCount(agentRunsSection.rows.length);
  // The traced run: its agent, errors, when, how long and tokens.
  await expect(rows.first()).toContainText('Claude Code · 4 errors');
  await expect(rows.first()).toContainText('2 days ago · 1 m · 8.5k tokens');
  await expect(rows.first().getByRole('link')).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-1/runs/sess-cc-1',
  );
  // The log-only run reported no tokens and had no errors: neither is written.
  await expect(rows.last()).toHaveText('Cursor3 days ago · 20 s');
  expect(seen.urls.filter((u) => u.endsWith('/items/SHIP-1/runs'))).toHaveLength(1);
});

test('RailAgentRuns_WithNoRuns_ShouldSaySo', async ({ mount, page }) => {
  await stubInspection(page, { section: empty });
  await mount(<RailAgentRuns project="SHIP" itemKey="SHIP-1" />);

  await expect(page.getByTestId(ids.railAgentRunsNote)).toHaveText(
    'None found in the last 30 days.',
  );
  await expect(page.getByTestId(ids.railAgentRunsRow)).toHaveCount(0);
});

test('RailAgentRuns_WhenStoreUnreachable_ShouldSaySo', async ({ mount, page }) => {
  await stubInspection(page, { section: { ...empty, storeReachable: false, problem: 'Down.' } });
  await mount(<RailAgentRuns project="SHIP" itemKey="SHIP-1" />);

  await expect(page.getByTestId(ids.railAgentRunsNote)).toHaveText(
    "Can't reach the trace store right now.",
  );
});
