import { expect, test } from '@playwright/experimental-ct-react';
import { mei, priya, story } from '../ct/fixtures';
import { SideRailHarness } from '../ct/SideRailHarness';
import { stubApi } from '../ct/stubApi';
import { attachment, change, revision, stubSide } from '../ct/stubSide';
import { componentTestIds } from '../../../components/testIds';
import { workItemTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const files = [
  attachment('a1', 'saved-card-picker.png', mei, 421888),
  attachment('a2', 'contract.pdf', null, 1258291),
];
const revisions = [
  revision(3, mei, [change('points', 3, 5)], { agentName: 'Claude Code (claude.ai)' }),
  revision(2, null, [change('title', 'Old', 'New')], { origin: 'jira' }),
  revision(1, priya, [
    change('title', null, 'Pay with saved card', null),
    change('description', null, 'x'.repeat(200), null),
  ]),
];

test('SideRail_WhenItemHasAttachments_ShouldListThemWithSizeUploaderAndDownloadLink', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubSide(page, mei, files, revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  const rows = screen.getByTestId(ids.attachment);
  await expect(rows).toHaveCount(2);
  await expect(screen.getByTestId(ids.attachmentsCount)).toHaveText('2');
  await expect(rows.nth(0).getByTestId(ids.attachmentDownload)).toHaveText('saved-card-picker.png');
  await expect(rows.nth(0).getByTestId(ids.attachmentDownload)).toHaveAttribute(
    'href',
    '/api/attachments/a1/content',
  );
  await expect(rows.nth(0).getByTestId(ids.attachmentSize)).toHaveText('412 KB');
  await expect(rows.nth(0).getByTestId(ids.attachmentBy)).toHaveText('Mei Lin, 2 days ago');
  await expect(rows.nth(1).getByTestId(ids.attachmentSize)).toHaveText('1.2 MB');
  await expect(rows.nth(1).getByTestId(ids.attachmentBy)).toHaveText('Jira import, 2 days ago');
});

test('SideRail_WhenNoAttachments_ShouldSayNoAttachments', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubSide(page, mei, [], revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.attachmentsEmpty)).toHaveText('No attachments.');
});

test('SideRail_WhenFileChosen_ShouldUploadItAndListIt', async ({ mount, page }) => {
  await stubApi(page, story);
  const side = await stubSide(page, mei, files, revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await screen
    .getByTestId(ids.attachmentFile)
    .setInputFiles({ name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from('hello') });

  await expect(screen.getByTestId(ids.attachment)).toHaveCount(3);
  await expect(screen.getByTestId(ids.attachment).nth(2)).toContainText('notes.txt');
  await expect(screen.getByTestId(ids.attachment).nth(2).getByTestId(ids.attachmentBy)).toHaveText(
    'Mei Lin, Just now',
  );
  expect(side.uploaded).toEqual(['notes.txt']);
  await expect(screen.getByTestId(componentTestIds.toast)).toContainText(
    'Uploaded notes.txt (5 B)',
  );
});

test('SideRail_WhenSeveralFilesChosen_ShouldUploadEachAndSayHowMany', async ({ mount, page }) => {
  await stubApi(page, story);
  const side = await stubSide(page, mei, files, revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.attachmentFile).setInputFiles([
    { name: 'one.txt', mimeType: 'text/plain', buffer: Buffer.from('1') },
    { name: 'two.txt', mimeType: 'text/plain', buffer: Buffer.from('22') },
  ]);

  await expect(screen.getByTestId(ids.attachment)).toHaveCount(4);
  expect(side.uploaded).toEqual(['one.txt', 'two.txt']);
  await expect(screen.getByTestId(componentTestIds.toast)).toContainText('Uploaded 2 files');
});

test('SideRail_WhenUploadRefused_ShouldShowTheServersMessageAndKeepTheList', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  const side = await stubSide(page, mei, files, revisions);
  side.refuseNext('An attachment can be at most 25 MB.');
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.attachmentFile).setInputFiles({
    name: 'big.bin',
    mimeType: 'application/octet-stream',
    buffer: Buffer.from('x'),
  });

  await expect(screen.getByTestId(ids.attachmentError)).toHaveText(
    'An attachment can be at most 25 MB.',
  );
  await expect(screen.getByTestId(ids.attachment)).toHaveCount(2);
  await expect(screen.getByTestId(componentTestIds.toast)).toHaveCount(0);
});

test('SideRail_WhenAttachmentRemoved_ShouldDropItFromTheList', async ({ mount, page }) => {
  await stubApi(page, story);
  const side = await stubSide(page, mei, files, revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.attachment).nth(0).getByTestId(ids.attachmentArchive).click();

  await expect(screen.getByTestId(ids.attachment)).toHaveCount(1);
  await expect(screen.getByTestId(ids.attachmentDownload)).toHaveText('contract.pdf');
  expect(side.archived).toEqual(['a1']);
  await expect(screen.getByTestId(componentTestIds.toast)).toContainText(
    'Removed saved-card-picker.png',
  );
});

test('SideRail_WhenHistoryOpened_ShouldListRevisionsNewestFirstWithWhoAndWhat', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubSide(page, mei, files, revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  const rows = screen.getByTestId(ids.revision);
  await expect(rows).toHaveCount(3);
  await expect(screen.getByTestId(ids.historyHint)).toContainText('Every save is one revision');
  await expect(screen.getByTestId(ids.historyCount)).toHaveText('3 revisions');
  await expect(rows.nth(0).getByTestId(ids.revisionNumber)).toHaveText('3');
  await expect(rows.nth(0).getByTestId(ids.revisionWho)).toHaveText(
    'Mei Lin via Claude Code (claude.ai)',
  );
  await expect(rows.nth(0).getByTestId(ids.revisionChange)).toHaveText('Story points3 → 5');
  await expect(rows.nth(0).getByTestId(ids.revisionFromJira)).toHaveCount(0);
  await expect(rows.nth(1).getByTestId(ids.revisionWho)).toHaveText('Jira import');
  await expect(rows.nth(1).getByTestId(ids.revisionFromJira)).toHaveText('from Jira');
  await expect(rows.nth(1).getByTestId(ids.revisionChange)).toHaveText('TitleOld → New');
  await expect(rows.nth(2).getByTestId(ids.revisionWho)).toHaveText('Priya Nair');
  await expect(rows.nth(2).getByTestId(ids.revisionWhen)).toHaveText('2 days ago');
  await expect(rows.nth(2).getByTestId(ids.revisionChange).nth(0)).toHaveText(
    'Titlenone → Pay with saved card',
  );
  await expect(rows.nth(2).getByTestId(ids.revisionChange).nth(1)).toHaveText('Descriptionchanged');
});

test('SideRail_WhenAWebhookClosedTheItem_ShouldNameThePullRequestAndMarkIt', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubSide(page, mei, files, [
    revision(4, null, [change('state', 'Active', 'Closed')], {
      origin: 'webhook',
      webhook: 'GitHub acme/storefront#12',
    }),
  ]);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  const row = screen.getByTestId(ids.revision);
  await expect(row.getByTestId(ids.revisionWho)).toHaveText('GitHub acme/storefront#12');
  await expect(row.getByTestId(ids.revisionWebhook)).toHaveText('webhook');
  await expect(row.getByTestId(ids.revisionFromJira)).toHaveCount(0);
});

test('SideRail_WhenItemIsNotImported_ShouldNotShowImportedFrom', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubSide(page, mei, files, revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.history)).toBeVisible();
  await expect(screen.getByTestId(ids.importedFrom)).toHaveCount(0);
});

test('SideRail_WhenItemWasImported_ShouldShowTheOriginalSystemIdAndLink', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    ...story,
    originalSystem: {
      system: 'jira',
      id: 'PLATFORM-412',
      url: 'https://jira.example.com/browse/PLATFORM-412',
    },
  });
  await stubSide(page, mei, files, revisions);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.importedFrom)).toContainText('Imported from Jira');
  const link = screen.getByTestId(ids.importedFromLink);
  await expect(link).toHaveText('PLATFORM-412 ↗');
  await expect(link).toHaveAttribute('href', 'https://jira.example.com/browse/PLATFORM-412');
  await expect(screen.getByTestId(ids.importedFromHost)).toHaveText('jira.example.com');
  await expect(screen.getByTestId(ids.importedFromHint)).toContainText(
    'Re-running the import matches on this reference and updates SHIP-141 instead of making a copy.',
  );
});

test('SideRail_WhenFeatureFieldsChanged_ShouldShowTheirLabelsAndNoRawTimestamp', async ({
  mount,
  page,
}) => {
  const instant = '2026-10-25T08:00:00Z';
  await stubApi(page, story);
  await stubSide(page, mei, files, [
    revision(2, mei, [
      change('goal', null, 'g'.repeat(200), null),
      change('statusLine', 'Staging works.', 'Live for 10%.', null),
      change('statusVerifiedAt', '2026-10-11T08:00:00Z', instant, [
        '2026-10-11 08:00 UTC',
        '2026-10-25 08:00 UTC',
      ]),
    ]),
  ]);
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  const changes = screen.getByTestId(ids.revision).getByTestId(ids.revisionChange);
  await expect(changes.nth(0)).toHaveText('Goalchanged');
  await expect(changes.nth(1)).toHaveText('StatusStaging works. → Live for 10%.');
  await expect(changes.nth(2)).toHaveText(
    'Status verified2026-10-11 08:00 UTC → 2026-10-25 08:00 UTC',
  );
  await expect(changes.nth(2)).not.toContainText(instant);
});
