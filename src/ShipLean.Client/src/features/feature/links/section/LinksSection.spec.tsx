import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../../workItem/ct/fixtures';
import { workItemHarnessIds as harness } from '../../../workItem/ct/harnessIds';
import { stubApi } from '../../../workItem/ct/stubApi';
import { stubComments } from '../../../workItem/ct/stubDetails';
import { stubSide } from '../../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../../workItem/ct/WorkItemFormHarness';
import { feature, linkCards, stubFeature } from '../../ct/stubFeature';
import { featureTestIds as ids } from '../../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function stubAll(page: StubbedPage, cards = linkCards) {
  await stubApi(page, feature);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  return stubFeature(page, { cards });
}

// The fixture without the card that is due for a refresh.
const settled = {
  ...linkCards,
  pages: linkCards.pages.filter((card) => !card.refreshDue),
};

test('LinksSection_ShouldGroupCodeAndPagesTwoAcross', async ({ mount, page }) => {
  await stubAll(page, settled);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const links = screen.getByTestId(ids.links);

  await expect(links.getByTestId(ids.linksCount)).toHaveText('5');
  await expect(links.getByTestId(ids.linkWorkItemRow)).toHaveCount(1);
  await expect(links.getByTestId(ids.linkWorkItemRow)).toContainText('SHIP-142');
  await expect(links.getByTestId(ids.linkWorkItemRow)).toContainText('Blocked by');
  const groups = links.getByTestId(ids.linkGroup);
  await expect(groups).toHaveCount(2);
  const code = groups.nth(0);
  await expect(code).toHaveAttribute('data-group', 'code');
  await expect(code.getByTestId(ids.linkCard)).toHaveCount(3);
  await expect(groups.nth(1).getByTestId(ids.linkCard)).toHaveCount(1);

  const [first, second] = await code
    .getByTestId(ids.linkCard)
    .evaluateAll((nodes) => nodes.slice(0, 2).map((node) => node.getBoundingClientRect()));
  expect(second!.top).toBe(first!.top);
  expect(second!.left).toBeGreaterThan(first!.right - 1);

  const open = code.getByTestId(ids.linkCard).nth(0);
  await expect(open.getByTestId(ids.linkCardState)).toHaveText('Open');
  await expect(open.getByTestId(ids.linkCardNumber)).toHaveText('#482');
  await expect(open.getByTestId(ids.linkCardSource)).toHaveText('live · Today');
  await expect(open.getByTestId(ids.linkCardNote)).toContainText('Needs SHIP-142 first');
  await expect(open.getByTestId(ids.linkCardImage)).toHaveAttribute('src', /\/image$/);
  const webhook = code.getByTestId(ids.linkCard).nth(2);
  await expect(webhook.getByTestId(ids.linkCardSource)).toHaveText('GitHub webhook · Today');
});

test('LinkCard_WhenReported_ShouldShowReporterAndAge', async ({ mount, page }) => {
  await stubAll(page, settled);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const card = screen.getByTestId(ids.linkCard).filter({ hasText: 'Rotate vault keys' });

  await expect(card.getByTestId(ids.linkCardState)).toHaveText('Merged');
  await expect(card.getByTestId(ids.linkCardSource)).toHaveText(
    'reported by Claude Code (claude.ai) · Yesterday',
  );
});

test('LinkCard_WhenFallback_ShouldShowDomainAndTitle', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const card = screen.getByTestId(ids.linkCard).filter({ hasText: 'down.example.com' });

  await expect(card.getByTestId(ids.linkCardTitle)).toHaveText('https://down.example.com/spec');
  await expect(card.getByTestId(ids.linkCardLocation)).toHaveText('down.example.com · no preview');
  await expect(card.getByTestId(ids.linkCardNote)).toContainText('Server was down when added');
  await expect(card.getByTestId(ids.linkCardState)).toHaveCount(0);
});

test('LinksSection_WhenRefreshDue_ShouldCallRefreshOnce', async ({ mount, page }) => {
  const stub = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  await expect(screen.getByTestId(ids.linkCard)).toHaveCount(5);

  await expect.poll(() => stub.calls.filter((c) => c.call === 'refresh').length).toBe(1);
  await expect(screen.getByTestId(ids.linkCard)).toHaveCount(5);
  expect(stub.calls.filter((c) => c.call === 'refresh')).toHaveLength(1);
});

test('LinkCard_WhenRemoved_ShouldDisappear', async ({ mount, page }) => {
  const stub = await stubAll(page, settled);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const card = screen.getByTestId(ids.linkCard).filter({ hasText: 'Rotate vault keys' });

  await card.getByRole('button', { name: 'Link actions' }).click();
  await card.getByTestId(ids.linkCardRemove).click();

  await expect(screen.getByTestId(ids.linkCard)).toHaveCount(3);
  await expect(card).toHaveCount(0);
  expect(stub.calls.some((c) => c.call === 'archiveLinkCard')).toBe(true);
});

test('LinksSection_WhenLoadFails_ShouldShowRetry', async ({ mount, page }) => {
  const stub = await stubAll(page, settled);
  stub.failNext('linkCards', 500, { error: 'Something went wrong.', code: 'internal_error' });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await expect(screen.getByTestId(ids.linksLoadFailed)).toBeVisible();
  await screen.getByTestId(ids.linksRetry).click();

  await expect(screen.getByTestId(ids.linksLoadFailed)).toHaveCount(0);
  await expect(screen.getByTestId(ids.linkCard)).toHaveCount(4);
});

test('LinkCard_WhenRemoveFails_ShouldKeepCardAndShowError', async ({ mount, page }) => {
  const stub = await stubAll(page, settled);
  stub.failNext('archiveLinkCard', 404, { error: 'Link not found.', code: 'not_found' });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const card = screen.getByTestId(ids.linkCard).filter({ hasText: 'Rotate vault keys' });

  await card.getByRole('button', { name: 'Link actions' }).click();
  await card.getByTestId(ids.linkCardRemove).click();

  await expect(card.getByTestId(ids.linkCardError)).toHaveText('Link not found.');
  await expect(card).toHaveCount(1);
});

test('LinkCard_WhenEditNoteFails_ShouldKeepDraft', async ({ mount, page }) => {
  const stub = await stubAll(page, settled);
  stub.failNext('editLinkCard', 400, {
    error: 'A note is one line.',
    code: 'validation',
    field: 'note',
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  // By position: while editing, the title is an input value, not text to filter on.
  const card = screen.getByTestId(ids.linkCard).nth(1);

  await card.getByRole('button', { name: 'Link actions' }).click();
  await card.getByTestId(ids.linkCardEdit).click();
  await card.getByTestId(ids.linkCardNoteInput).fill('my draft note');
  await card.getByTestId(ids.linkCardSave).click();

  await expect(card.getByTestId(ids.linkCardError)).toHaveText('A note is one line.');
  await expect(card.getByTestId(ids.linkCardNoteInput)).toHaveValue('my draft note');

  await card.getByTestId(ids.linkCardSave).click();
  await expect(card.getByTestId(ids.linkCardNote)).toContainText('my draft note');
});

test('WorkItemLinkRow_WhenClicked_ShouldOpenItemModal', async ({ mount, page }) => {
  await stubAll(page, settled);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.linkWorkItemRow).getByTestId('item-link').click();

  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-100');
  await expect(screen.getByTestId(harness.search)).toHaveText('?item=SHIP-142');
});
