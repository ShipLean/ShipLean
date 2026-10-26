import { expect, test } from '@playwright/experimental-ct-react';
import type { LinkDto } from '../../../api/workItems';
import { bug, story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const target = { ...bug, id: 'w120', key: 'SHIP-120', title: 'Vault outage' };
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
const web: LinkDto = {
  id: 'l2',
  type: 'url',
  workItemId: null,
  key: null,
  title: 'Card vault notes',
  url: 'https://wiki.example.com/card-vault',
  card: null,
  autoLinked: null,
};
const linked = { ...story, links: [related, web] };

test('LinksEditor_WhenItemHasLinks_ShouldListThemWithTypes', async ({ mount, page }) => {
  await stubApi(page, linked);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await expect(screen.getByTestId(ids.linksCount)).toHaveText('2');
  const rows = screen.getByTestId(ids.linkRow);
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText('Related');
  await expect(rows.nth(0)).toContainText('SHIP-144');
  await expect(rows.nth(0)).toContainText('Saved cards list');
  await expect(rows.nth(1)).toContainText('External URL');
  await expect(rows.nth(1).getByRole('link')).toHaveAttribute(
    'href',
    'https://wiki.example.com/card-vault',
  );
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 7');
});

test('LinksEditor_WhenAWebhookReportedOnAPullRequest_ShouldShowItsStateAndAuto', async ({
  mount,
  page,
}) => {
  const pull: LinkDto = {
    id: 'l3',
    type: 'url',
    workItemId: null,
    key: null,
    title: 'SHIP-141 Saved card picker',
    url: 'https://github.com/acme/storefront/pull/418',
    card: {
      state: 'merged',
      title: 'SHIP-141 Saved card picker',
      asOf: '2026-10-25T09:30:00+00:00',
      asOfLabel: '4 min ago',
      source: 'github',
    },
    autoLinked: { via: 'github', matchedOn: 'branch' },
  };
  await stubApi(page, { ...story, links: [web, pull] });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  const rows = screen.getByTestId(ids.linkRow);
  await expect(rows.nth(0).getByTestId(ids.linkCard)).toHaveCount(0);
  await expect(rows.nth(1).getByTestId(ids.linkCard)).toHaveText('Merged');
  await expect(rows.nth(1).getByTestId(ids.linkCard)).toHaveAttribute(
    'title',
    'GitHub webhook · 4 min ago · SHIP-141 Saved card picker',
  );
  await expect(rows.nth(1).getByTestId(ids.linkAutoLinked)).toHaveText('auto');
});

test('LinksEditor_WhenNoLinks_ShouldSayNoLinks', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await expect(screen.getByTestId(ids.linksEmpty)).toHaveText('No links.');
});

test('LinksEditor_WhenWorkItemLinkAdded_ShouldMakeTheFormDirtyAndSendAddLinks', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, story, undefined, { others: [target] });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkType).selectOption('blockedBy');
  await screen.getByTestId(ids.linkKey).fill('ship-120');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkRow)).toContainText('Vault outage');
  await expect(screen.getByTestId(ids.linkRow)).toContainText('unsaved');
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Links');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches).toHaveLength(1);
  expect(api.patches[0].changes.addLinks).toEqual([
    { type: 'blockedBy', workItemId: 'w120', key: 'SHIP-120', title: 'Vault outage', url: null },
  ]);
  expect(api.patches[0].changes.removeLinkIds).toEqual([]);
  expect(api.patches[0].changes.title).toBeNull();
});

test('LinksEditor_WhenUrlLinkAdded_ShouldSendUrlAndTitle', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkType).selectOption('url');
  await screen.getByTestId(ids.linkUrl).fill('https://crash.example.com/881');
  await screen.getByTestId(ids.linkTitle).fill('Crash report');
  await screen.getByTestId(ids.linkSubmit).click();
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.addLinks).toEqual([
    {
      type: 'url',
      workItemId: null,
      key: null,
      url: 'https://crash.example.com/881',
      title: 'Crash report',
    },
  ]);
});

test('LinksEditor_WhenUrlIsNotWeb_ShouldRefuseAndSayWhy', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkType).selectOption('url');
  await screen.getByTestId(ids.linkUrl).fill('javascript:alert(1)');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkError)).toHaveText(
    'Enter a full URL starting with https://',
  );
  await expect(screen.getByTestId(ids.linkRow)).toHaveCount(0);
});

test('LinksEditor_WhenKeyIsNotInTheProject_ShouldSayNotFound', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkKey).fill('SHIP-999');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkError)).toHaveText(
    "SHIP-999 isn't a work item in this project.",
  );
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 7');
});

test('LinksEditor_WhenLinkingToItself_ShouldRefuse', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkKey).fill('SHIP-141');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkError)).toContainText("can't link to itself");
});

test('LinksEditor_WhenSameLinkAddedTwice_ShouldRefuse', async ({ mount, page }) => {
  await stubApi(page, linked, undefined, {
    others: [{ ...target, id: 'w144', key: 'SHIP-144', title: 'Saved cards list' }],
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkType).selectOption('related');
  await screen.getByTestId(ids.linkKey).fill('SHIP-144');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkError)).toHaveText(
    'That link is already on this work item.',
  );
  await expect(screen.getByTestId(ids.linkRow)).toHaveCount(2);
});

test('LinksEditor_WhenLinkRemoved_ShouldSendItsId', async ({ mount, page }) => {
  const api = await stubApi(page, linked);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkRemove).first().click();

  await expect(screen.getByTestId(ids.linkRow)).toHaveCount(1);
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Links');
  await screen.getByTestId(ids.save).click();
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.removeLinkIds).toEqual(['l1']);
  expect(api.patches[0].changes.addLinks).toEqual([]);
});

test('LinksEditor_WhenLinkEditedBackToWhatItWas_ShouldBeClean', async ({ mount, page }) => {
  await stubApi(page, linked, undefined, {
    others: [{ ...target, id: 'w144', key: 'SHIP-144', title: 'Saved cards list' }],
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkEdit).first().click();
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkForm)).toHaveCount(0);
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 7');
  await expect(screen.getByTestId(ids.links).getByTestId(ids.changedDot)).toHaveCount(0);
});

test('LinksEditor_WhenLinkAdded_ShouldMarkTheLinksChanged', async ({ mount, page }) => {
  await stubApi(page, story, undefined, { others: [target] });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);
  await expect(screen.getByTestId(ids.links).getByTestId(ids.changedDot)).toHaveCount(0);

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkKey).fill('SHIP-120');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkRow)).toHaveCount(1);
  await expect(screen.getByTestId(ids.links).getByTestId(ids.changedDot)).toBeVisible();
});

test('LinksEditor_WhenLinksAreMixed_ShouldGroupThemByTypeInTheOrderOfTheTypes', async ({
  mount,
  page,
}) => {
  await stubApi(page, { ...story, links: [web, related] }, undefined, { others: [target] });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);
  const rows = screen.getByTestId(ids.linkRow);
  await expect(rows.nth(0)).toHaveAttribute('data-link-type', 'related');
  await expect(rows.nth(1)).toHaveAttribute('data-link-type', 'url');

  await screen.getByTestId(ids.linkAdd).click();
  await screen.getByTestId(ids.linkType).selectOption('blockedBy');
  await screen.getByTestId(ids.linkKey).fill('SHIP-120');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toHaveAttribute('data-link-type', 'blockedBy');
  await expect(rows.nth(1)).toHaveAttribute('data-link-type', 'related');
  await expect(rows.nth(2)).toHaveAttribute('data-link-type', 'url');
});

test('LinksEditor_WhenLinkTypeEdited_ShouldRemoveTheOldAndAddTheNew', async ({ mount, page }) => {
  const api = await stubApi(page, linked, undefined, {
    others: [{ ...target, id: 'w144', key: 'SHIP-144', title: 'Saved cards list' }],
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);

  await screen.getByTestId(ids.linkEdit).first().click();
  await screen.getByTestId(ids.linkType).selectOption('successor');
  await screen.getByTestId(ids.linkSubmit).click();
  await expect(screen.getByTestId(ids.linkRow).first()).toContainText('Successor');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.removeLinkIds).toEqual(['l1']);
  expect(api.patches[0].changes.addLinks).toEqual([
    {
      type: 'successor',
      workItemId: 'w144',
      key: 'SHIP-144',
      title: 'Saved cards list',
      url: null,
    },
  ]);
});

test('LinksEditor_WhenDiscarded_ShouldRestoreSavedLinks', async ({ mount, page }) => {
  await stubApi(page, linked);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);
  await screen.getByTestId(ids.linkRemove).first().click();

  await screen.getByTestId(ids.discard).click();

  await expect(screen.getByTestId(ids.linkRow)).toHaveCount(2);
});
