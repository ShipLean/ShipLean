import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../../workItem/ct/fixtures';
import { stubApi } from '../../../workItem/ct/stubApi';
import { stubComments } from '../../../workItem/ct/stubDetails';
import { stubSide } from '../../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../../workItem/ct/WorkItemFormHarness';
import {
  feature,
  linkCards,
  linkPreview,
  privateLinkPreview,
  stubFeature,
} from '../../ct/stubFeature';
import { featureTestIds as ids } from '../../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

// The fixture without the card that is due for a refresh, so no refresh call mixes into `calls`.
const settled = { ...linkCards, pages: linkCards.pages.filter((card) => !card.refreshDue) };

async function stubAll(page: StubbedPage, preview = linkPreview) {
  await stubApi(page, feature);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  return stubFeature(page, { cards: settled, preview });
}

test('AddLink_WhenGitHubUrlPasted_ShouldShowLivePreview', async ({ mount, page }) => {
  const { calls } = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const root = screen.getByTestId(ids.addLink);
  await root.getByTestId(ids.addLinkOpen).click();
  await root.getByTestId(ids.addLinkUrl).fill(linkPreview.url);
  await root.getByTestId(ids.addLinkUrl).blur();

  await expect(root.getByTestId(ids.addLinkMessage)).toHaveText(linkPreview.message);
  await expect(root.getByTestId(ids.addLinkPreviewTitle)).toHaveText(linkPreview.title);
  await expect(root.getByTestId(ids.addLinkTitle)).toHaveAttribute(
    'placeholder',
    linkPreview.title,
  );
  await root.getByTestId(ids.addLinkNote).fill('Needs the picker');
  await root.getByTestId(ids.addLinkSubmit).click();

  await expect(root.getByTestId(ids.addLinkForm)).toHaveCount(0);
  await expect(screen.getByTestId(ids.linksCount)).toHaveText('6');
  expect(calls.find((c) => c.call === 'addLinkCard')?.body).toEqual({
    url: linkPreview.url,
    note: 'Needs the picker',
  });
});

test('AddLink_WhenPrivateHost_ShouldShowPrivateMessage', async ({ mount, page }) => {
  await stubAll(page, privateLinkPreview);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const root = screen.getByTestId(ids.addLink);
  await root.getByTestId(ids.addLinkOpen).click();
  await root.getByTestId(ids.addLinkUrl).fill(privateLinkPreview.url);
  await root.getByTestId(ids.addLinkUrl).blur();

  await expect(root.getByTestId(ids.addLinkMessage)).toHaveText(privateLinkPreview.message);
  await expect(root.getByTestId(ids.addLinkMessage)).toContainText('Private address');
  await expect(root.getByTestId(ids.addLinkSubmit)).toBeEnabled();
});

test('AddLink_WhenWorkItemTab_ShouldAddRelation', async ({ mount, page }) => {
  const { calls } = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const root = screen.getByTestId(ids.addLink);
  await root.getByTestId(ids.addLinkOpen).click();
  await root.getByTestId(ids.addLinkTabWorkItem).click();
  await root.getByTestId(ids.addLinkKey).fill('ship-150');
  await root.getByTestId(ids.addLinkType).selectOption('duplicate');
  await root.getByTestId(ids.addLinkSubmit).click();

  await expect(root.getByTestId(ids.addLinkForm)).toHaveCount(0);
  await expect(screen.getByTestId(ids.linkWorkItemRow)).toHaveCount(2);
  expect(calls.find((c) => c.call === 'addWorkItemLink')?.body).toEqual({
    targetKey: 'SHIP-150',
    type: 'duplicate',
  });
});

test('AddLink_WhenDuplicate_ShouldShowInlineError', async ({ mount, page }) => {
  const { failNext } = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const root = screen.getByTestId(ids.addLink);
  await root.getByTestId(ids.addLinkOpen).click();
  await root.getByTestId(ids.addLinkTabWorkItem).click();
  await root.getByTestId(ids.addLinkKey).fill('SHIP-142');
  failNext('addWorkItemLink', 409, {
    error: 'SHIP-142 is already linked.',
    code: 'link_duplicate',
    field: 'targetKey',
  });
  await root.getByTestId(ids.addLinkSubmit).click();

  await expect(root.getByTestId(ids.addLinkError)).toHaveText('SHIP-142 is already linked.');
  await expect(root.getByTestId(ids.addLinkForm)).toBeVisible();
  await expect(root.getByTestId(ids.addLinkKey)).toHaveValue('SHIP-142');
});

test('AddLink_WhenInvalidUrl_ShouldShowInlineError', async ({ mount, page }) => {
  const { failNext } = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const root = screen.getByTestId(ids.addLink);
  await root.getByTestId(ids.addLinkOpen).click();
  await root.getByTestId(ids.addLinkUrl).fill('ftp://example.com/x');
  failNext('addLinkCard', 400, {
    error: 'Enter a full http or https address.',
    code: 'link_invalid_url',
    field: 'url',
  });
  await root.getByTestId(ids.addLinkSubmit).click();

  await expect(root.getByTestId(ids.addLinkError)).toHaveText(
    'Enter a full http or https address.',
  );
  await expect(root.getByTestId(ids.addLinkForm)).toBeVisible();
});

test('AddLink_WhenPreviewFails_ShouldStillAllowAdd', async ({ mount, page }) => {
  const { failNext, calls } = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const root = screen.getByTestId(ids.addLink);
  await root.getByTestId(ids.addLinkOpen).click();
  failNext('preview', 500, { error: 'Preview failed.', code: 'internal_error' });
  await root.getByTestId(ids.addLinkUrl).fill('https://example.com/spec');
  const previewed = page.waitForResponse('**/link-cards/preview');
  await root.getByTestId(ids.addLinkUrl).blur();
  await previewed;

  await expect(root.getByTestId(ids.addLinkPreview)).toHaveCount(0);
  await root.getByTestId(ids.addLinkSubmit).click();
  await expect(root.getByTestId(ids.addLinkForm)).toHaveCount(0);
  expect(calls.some((c) => c.call === 'addLinkCard')).toBe(true);
});
