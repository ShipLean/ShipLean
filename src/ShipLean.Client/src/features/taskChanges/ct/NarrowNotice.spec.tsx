import { expect, test } from '@playwright/experimental-ct-react';
import { NarrowNotice } from '../NarrowNotice';
import { taskChangesTestIds as ids } from '../testIds';

test('NarrowNotice_WhenShown_ShouldLinkBackToTheItem', async ({ mount, page }) => {
  await mount(<NarrowNotice projectKey="SHIP" itemKey="SHIP-171" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-171/changes' },
  });
  await expect(page.getByTestId(ids.narrowNotice)).toContainText('Changes need a wider screen.');
  await expect(page.getByTestId(ids.narrowBack)).toHaveAttribute('href', '/p/SHIP/items/SHIP-171');
});
