import { expect, test } from '@playwright/experimental-ct-react';
import { quickSearchDialogHarnessIds as harness } from './ct/harnessIds';
import { QuickSearchDialogHarness } from './ct/QuickSearchDialogHarness';
import { quickSearchTestIds as ids } from './testIds';
import type { QuickSearchResults } from './types';

// Quick search on a phone (SHIP-713, C9): the whole screen, the field at the top with Cancel beside it, rows of at
// least --touch-target with the key under the title, and no key caps.

const nothing = (): void => undefined;

const results: QuickSearchResults = {
  status: 'ready',
  note: null,
  retry: null,
  groups: [
    {
      id: 'items',
      title: 'Work items',
      rows: [
        {
          kind: 'item',
          id: 'SHIP-41',
          item: {
            key: 'SHIP-41',
            title: 'Proxy sign-in for installs',
            type: 'feature',
            state: 'Active',
            stateCategory: 'inProgress',
            parentPath: ['SHIP-12'],
            projectKey: 'SHIP',
          },
          match: null,
        },
      ],
    },
    {
      id: 'sprints',
      title: 'Sprints',
      rows: [
        {
          kind: 'sprint',
          id: '/p/SHIP/sprints/s24',
          name: 'Sprint 24',
          goal: 'Ship the proxy',
          datesLabel: '3–14 Nov',
          current: true,
          projectKey: 'SHIP',
          open: nothing,
        },
      ],
    },
    {
      id: 'actions',
      title: 'Actions',
      rows: [
        {
          kind: 'action',
          id: 'create-bug',
          glyph: '+',
          label: 'Create bug',
          detail: 'in SHIP',
          run: nothing,
        },
      ],
    },
  ],
};

test('QuickSearch_WhenOpenOnPhone_ShouldFillTheScreenWithCancelBesideTheField', async ({
  mount,
  page,
}) => {
  await mount(<QuickSearchDialogHarness results={results} initialQuery="proxy" />);
  const dialog = page.getByTestId(ids.dialog);
  const input = page.getByTestId(ids.input);
  const cancel = page.getByTestId(ids.cancel);

  expect(await dialog.boundingBox()).toEqual({ x: 0, y: 0, width: 390, height: 844 });
  const field = (await input.boundingBox())!;
  const button = (await cancel.boundingBox())!;
  expect(button.x).toBeGreaterThanOrEqual(field.x + field.width);
  expect(Math.abs(button.y + button.height / 2 - (field.y + field.height / 2))).toBeLessThan(2);
  expect(button.height).toBeGreaterThanOrEqual(32);
  // No key caps: innerText leaves out what is not displayed.
  await expect(dialog).not.toContainText('Esc', { useInnerText: true });
  await expect(page.getByTestId(ids.footer)).toBeHidden();

  await cancel.tap();

  await expect(page.getByTestId(harness.closed)).toHaveText('1');
});

test('QuickSearch_WhenRowsListedOnPhone_ShouldPutTheKeyUnderTheTitle', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results} initialQuery="proxy" />);
  const item = page.getByTestId(ids.option).and(page.locator('[data-key="SHIP-41"]'));
  await expect(item).toBeVisible();

  const title = (await item.getByTestId(ids.optionLabel).boundingBox())!;
  const key = (await item.getByTestId(ids.optionKey).boundingBox())!;
  expect(key.y).toBeGreaterThanOrEqual(title.y + title.height - 1);
  expect(Math.abs(key.x - title.x)).toBeLessThan(2);
  for (const option of await page.getByTestId(ids.option).all()) {
    expect((await option.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
  await expect(page.getByTestId(ids.group)).toHaveText(['Work items', 'Sprints', 'Actions']);
});
