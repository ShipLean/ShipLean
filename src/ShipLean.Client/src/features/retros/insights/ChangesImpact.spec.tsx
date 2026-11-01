import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroImpactDto, RetroInsightsDto } from '../../../api/retros';
import { retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { ChangesImpact } from './ChangesImpact';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const open = { hooksConfig: { route: '/p/SHIP/retros/insights?view=impact' } };
const card = (page: Page, key: string) =>
  page.getByTestId(ids.retroImpactCard).and(page.locator(`[data-action-key="${key}"]`));
const byVerdict = (verdict: string) => retroInsights.impact.find((i) => i.verdict === verdict)!;

test('ChangesImpact_WhenMounted_ShouldShowACardPerClosedAction', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ChangesImpact project={insightsProject} insights={retroInsights} />, open);
  const cards = page.getByTestId(ids.retroImpactCard);
  await expect(cards).toHaveCount(retroInsights.impact.length);
  const keys = await cards.evaluateAll((els) => els.map((e) => e.getAttribute('data-action-key')));
  expect(keys).toEqual(retroInsights.impact.map((i) => i.action.key));
});

test('ChangesImpact_WhenMounted_ShouldDrawABarPerSprintAndALineAtEachAverage', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ChangesImpact project={insightsProject} insights={retroInsights} />, open);
  const helped = byVerdict('helped');
  const view = card(page, helped.action.key);
  await expect(view.locator('[data-bar]')).toHaveCount(retroInsights.sprints.length);
  await expect(view.locator('[data-line="before"]')).toHaveAttribute(
    'data-value',
    String(helped.beforeAvg),
  );
  await expect(view.locator('[data-line="after"]')).toHaveAttribute(
    'data-value',
    String(helped.afterAvg),
  );
  await expect(view.locator('[data-marker]')).toHaveCount(1);
});

test('ChangesImpact_WhenChangeKnown_ShouldShowItSigned', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ChangesImpact project={insightsProject} insights={retroInsights} />, open);
  const helped = byVerdict('helped');
  const worse = byVerdict('noEffect');
  await expect(card(page, helped.action.key).locator('[data-figure="change"]')).toContainText(
    `${Math.round(helped.changePct! * 100)}%`,
  );
  await expect(card(page, worse.action.key).locator('[data-figure="change"]')).toContainText(
    `+${Math.round(worse.changePct! * 100)}%`,
  );
});

test('ChangesImpact_WhenChangeIsNull_ShouldHideTheChange', async ({ mount, page }) => {
  await stubRetros(page);
  const insights: RetroInsightsDto = {
    ...retroInsights,
    impact: retroInsights.impact.map((i) => ({ ...i, changePct: null })),
  };
  await mount(<ChangesImpact project={insightsProject} insights={insights} />, open);
  await expect(page.locator('[data-figure="change"]')).toHaveCount(0);
  await expect(page.locator('[data-figure="before"]').first()).toBeVisible();
});

test('ChangesImpact_WhenTooEarly_ShouldSayTooEarlyAndDrawNoAfterLine', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ChangesImpact project={insightsProject} insights={retroInsights} />, open);
  const early = byVerdict('tooEarly');
  const view = card(page, early.action.key);
  await expect(view).toContainText('Too early');
  await expect(view.locator('[data-line="after"]')).toHaveCount(0);
  await expect(view.locator('[data-figure="after"] b')).toHaveText('–');
  await expect(view.locator('[data-callout="latest"]')).toHaveCount(0);
});

test('ChangesImpact_WhenNoEffect_ShouldShowTheLatestMentionAndItsFollowUp', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const worse = byVerdict('noEffect');
  const mention = {
    ...retroInsights.impact.find((i) => i.latestMention)!.latestMention!,
    text: 'Reviews still arrive on the last day',
  };
  const impact: RetroImpactDto[] = retroInsights.impact.map((i) =>
    i === worse ? { ...i, latestMention: mention } : i,
  );
  await mount(
    <ChangesImpact project={insightsProject} insights={{ ...retroInsights, impact }} />,
    open,
  );
  await expect(card(page, worse.action.key).locator('[data-callout="latest"]')).toContainText(
    mention.text,
  );
});

test('ChangesImpact_WhenClosedBeforeTheRange_ShouldPutTheMarkerAtTheLeftEdge', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const early = byVerdict('someEffect');
  const impact = retroInsights.impact.map((i) => (i === early ? { ...i, closedAt: -1 } : i));
  await mount(
    <ChangesImpact project={insightsProject} insights={{ ...retroInsights, impact }} />,
    open,
  );
  const view = card(page, early.action.key);
  await expect(view.locator('[data-bar][data-after="true"]')).toHaveCount(
    retroInsights.sprints.length,
  );
  await expect(view.locator('[data-line="before"]')).toHaveCount(0);
  await expect(view.locator('[data-marker]')).toHaveCount(1);
});

test('ChangesImpact_WhenNoActionClosed_ShouldSayNothingHere', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(
    <ChangesImpact project={insightsProject} insights={{ ...retroInsights, impact: [] }} />,
    open,
  );
  await expect(page.getByText('Nothing here.')).toBeVisible();
});

for (const theme of ['light', 'dark'] as const) {
  test(`ChangesImpact_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await stubRetros(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(
      <ChangesImpact project={insightsProject} insights={retroInsights} />,
      open,
    );
    await expect(view).toHaveScreenshot(`changes-impact.${theme}.png`, { animations: 'disabled' });
  });
}
