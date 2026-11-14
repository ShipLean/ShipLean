import { expect } from '@playwright/experimental-ct-react';
import { attachmentTestIds as ids } from '../../features/attachment/testIds';
import { touchTestIds } from '../../touch/testIds';
import { checkoutV2, designView, stubFeaturePage } from './fixtures/feature';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// The Markdown attachment's phone editor (SHIP-715, L715b): the toolbar in the top bar's place, the editor under it and
// the Save bar once there is a change. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/attachmentEdit.phone.spec.tsx.

phonePageShots([
  {
    name: 'attachment-edit',
    route: `/p/SHIP/items/SHIP-100/attachments/${designView.attachment.id}`,
    shell: false,
    arrange: async (page: Page) => {
      await stubFeaturePage(page, checkoutV2, designView);
    },
    ready: async (page: Page) => {
      await expect(page.getByTestId(ids.name)).toHaveText('design-v0.2.md');
    },
    act: async (page: Page) => {
      await page.getByTestId(ids.edit).tap();
      const input = page.getByTestId(ids.editorInput);
      await expect(input).toBeVisible();
      await input.fill(`${await input.inputValue()}\n\nOne more line.\n`);
      await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
    },
  },
]);
