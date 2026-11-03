import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../../components/testIds';
import { ToastHost } from '../../../../components/ToastHost';
import { retroInsights, stubRetros } from '../../ct/retroFixtures';
import { retrosTestIds as ids } from '../../testIds';
import { insightsProject } from '../ct/insightsProject';
import { RetroInsightsPanel } from '../RetroInsightsPanel';
import { MergeProposals } from './MergeProposals';

const open = { hooksConfig: { route: '/p/SHIP/retros/insights?view=themes' } };
const [proposal] = retroInsights.mergeProposals;

test('MergeProposals_ShouldShowFromIntoAndReason', async ({ mount }) => {
  const view = await mount(
    <MergeProposals project={insightsProject} proposals={retroInsights.mergeProposals} />,
  );
  const row = view.getByTestId(ids.retroMergeProposal);
  await expect(row).toHaveCount(retroInsights.mergeProposals.length);
  await expect(view).toContainText('1 merge proposed');
  await expect(row.first()).toContainText(proposal.from.name);
  await expect(row.first()).toContainText(proposal.into.name);
  await expect(row.first()).toContainText(proposal.reason);
  await expect(row.first()).toContainText(`${proposal.pointCount} points`);
});

test('MergeProposals_WhenNone_ShouldRenderNothing', async ({ mount }) => {
  const view = await mount(<MergeProposals project={insightsProject} proposals={[]} />);
  await expect(view.getByTestId(ids.retroMergeProposal)).toHaveCount(0);
  await expect(view).toBeEmpty();
});

test('MergeProposals_WhenMergeClicked_ShouldCallTheRouteAndRefetch', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroInsightsPanel project={insightsProject} />, open);
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(1);
  await page.getByTestId(ids.retroMergeProposalMerge).click();
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].method).toBe('POST');
  expect(stub.writes()[0].path).toBe(`/api/retros/theme-merge-proposals/${proposal.id}/merge`);
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(2);
});

test('MergeProposals_WhenKeepApartClicked_ShouldCallTheRoute', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(
    <MergeProposals project={insightsProject} proposals={retroInsights.mergeProposals} />,
  );
  await page.getByTestId(ids.retroMergeProposalKeep).click();
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].method).toBe('POST');
  expect(stub.writes()[0].path).toBe(`/api/retros/theme-merge-proposals/${proposal.id}/keep-apart`);
});

test('MergeProposals_WhenTheRouteFails_ShouldToastTheError', async ({ mount, page }) => {
  await stubRetros(page);
  await page.route(/\/api\/retros\/theme-merge-proposals\/[^/]+\/merge$/, (route) =>
    route.fulfill({
      status: 409,
      contentType: 'application/json',
      body: JSON.stringify({
        error: 'That merge proposal was already answered.',
        code: 'conflict',
      }),
    }),
  );
  await mount(
    <>
      <MergeProposals project={insightsProject} proposals={retroInsights.mergeProposals} />
      <ToastHost />
    </>,
  );
  await page.getByTestId(ids.retroMergeProposalMerge).click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('already answered');
  await expect(page.getByTestId(ids.retroMergeProposal)).toHaveCount(1);
});
