import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { DecisionDto } from '@shiplean/core/api/asks';
import { componentTestIds } from '../../components/testIds';
import { waiting } from './ct/fixtures';
import { RegionHarness } from './ct/RegionHarness';
import { stubAsks } from './ct/stubAsks';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';
import { DecisionDiscuss } from './DecisionDiscuss';

const item: AsksItem = {
  id: waiting.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
};
const d1 = waiting.decisions[0] as DecisionDto;

async function setUp(page: Page) {
  const stub = await stubAsks(page, waiting);
  const closed = { count: 0 };
  const view = (
    <RegionHarness>
      <DecisionDiscuss
        item={item}
        asks={waiting}
        decision={d1}
        onClose={() => {
          closed.count += 1;
        }}
      />
    </RegionHarness>
  );
  return { stub, closed, view };
}

test('DecisionDiscuss_WhenPosted_ShouldPostQuotedBodyToDiscussRouteAndClose', async ({
  mount,
  page,
}) => {
  const { stub, closed, view } = await setUp(page);
  const component = await mount(view);

  await expect(component).toContainText('Posts to Discussion with D1 quoted. D1 stays open.');
  await component.getByTestId(ids.discussInput).fill('Does the vault skip CVV for tokens?');
  await component.getByTestId(ids.discussPost).click();

  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0].url).toBe(`/api/work-items/${waiting.workItemId}/decisions/1/discuss`);
  expect(stub.writes[0].body).toEqual({
    body: `> **D1** · ${d1.question}: ${d1.currentAnswer}\n\nDoes the vault skip CVV for tokens?`,
  });
  await expect(page.getByTestId(componentTestIds.toast)).toHaveText(
    'Comment posted · D1 stays open',
  );
  await expect.poll(() => closed.count).toBe(1);
});

test('DecisionDiscuss_WhenBlank_ShouldDisablePost', async ({ mount, page }) => {
  const { view } = await setUp(page);
  const component = await mount(view);

  await expect(component.getByTestId(ids.discussPost)).toBeDisabled();
  await component.getByTestId(ids.discussInput).fill('  ');
  await expect(component.getByTestId(ids.discussPost)).toBeDisabled();
});

test('DecisionDiscuss_WhenRefused_ShouldShowErrorAndKeepText', async ({ mount, page }) => {
  const { stub, closed, view } = await setUp(page);
  stub.failNext(404, { error: 'D1 was not found.', code: 'not_found' });
  const component = await mount(view);

  await component.getByTestId(ids.discussInput).fill('Still here');
  await component.getByTestId(ids.discussPost).click();

  await expect(component.getByTestId(ids.discussError)).toHaveText('D1 was not found.');
  await expect(component.getByTestId(ids.discussInput)).toHaveValue('Still here');
  expect(closed.count).toBe(0);
});

for (const theme of ['light', 'dark'] as const) {
  test(`DecisionDiscuss_WhenRendered_ShouldMatchItsScreenshot_discuss_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const { view } = await setUp(page);
    const component = await mount(view);
    await component
      .getByTestId(ids.discussInput)
      .fill(
        '@Tom does the vault let us skip CVV for tokens it issued? If so I’d take “Only above £50”.',
      );
    await expect(component.getByTestId(ids.decisionDiscuss)).toHaveScreenshot(
      `decision-discuss.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
