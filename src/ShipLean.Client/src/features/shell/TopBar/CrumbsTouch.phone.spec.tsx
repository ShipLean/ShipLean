import { expect, test } from '@playwright/experimental-ct-react';
import markdownFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/attachments/attachment-markdown.json' with { type: 'json' };
import { stubShell } from '../../../pages/screenshots/shellStub';
import { shellTestIds as ids } from '../testIds';
import { Crumbs } from './Crumbs';

// The attachment page's crumb (SHIP-715 D8): on a phone the last crumb is the file's name, read from the page's own
// attachment query; on a desktop it keeps saying "Attachment".

const view = markdownFixture.markdownView;
const ROUTE = `/p/SHIP/items/SHIP-141/attachments/${view.attachment.id}`;

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

async function stubAttachment(page: Page) {
  await stubShell(page);
  await page.route(`**/api/attachments/${view.attachment.id}/markdown`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(view) }),
  );
}

test('Crumbs_WhenOnAttachmentRouteAtTouch_ShouldEndInFileName', async ({ mount, page }) => {
  await stubAttachment(page);
  await mount(<Crumbs />, { hooksConfig: { route: ROUTE } });

  await expect(page.getByTestId(ids.crumb)).toHaveText([
    'SHIP',
    'Backlogs',
    'SHIP-141',
    'vault-notes.md',
  ]);
});

test('Crumbs_WhenOnAttachmentRouteOnDesktop_ShouldSayAttachment', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubAttachment(page);
  const markdown = page.waitForResponse(`**/api/attachments/${view.attachment.id}/markdown`, {
    timeout: 1000,
  });
  await mount(<Crumbs />, { hooksConfig: { route: ROUTE } });

  await expect(page.getByTestId(ids.crumb)).toHaveText([
    'SHIP',
    'Backlogs',
    'SHIP-141',
    'Attachment',
  ]);
  // The desktop crumb never asks for the file: catches a name that only loses the race.
  await expect(markdown).rejects.toThrow();
});
