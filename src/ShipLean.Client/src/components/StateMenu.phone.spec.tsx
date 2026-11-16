import { expect, test } from '@playwright/experimental-ct-react';
import { Sheet } from '../touch/Sheet';
import { touchTestIds } from '../touch/testIds';
import { StateMenu } from './StateMenu';
import { componentTestIds as ids } from './testIds';

const THEMES = ['light', 'dark'] as const;

// Given out of category order, as a project's states can be ordered.
const states = [
  { id: 'new', name: 'New', category: 'proposed' as const },
  { id: 'done', name: 'Closed', category: 'completed' as const },
  { id: 'active', name: 'Active', category: 'inProgress' as const },
  { id: 'ready', name: 'Ready', category: 'proposed' as const },
  { id: 'resolved', name: 'Resolved', category: 'resolved' as const },
  { id: 'removed', name: 'Removed', category: 'removed' as const },
];

test('StateMenu_ShouldGroupStatesByCategoryAndMarkTheCurrent', async ({ mount, page }) => {
  const picked: string[] = [];
  await mount(
    <Sheet open title="State" onClose={() => undefined}>
      <StateMenu
        states={states}
        currentId="active"
        onSelect={(id) => picked.push(id)}
        onClose={() => undefined}
      />
    </Sheet>,
  );
  const menu = page.getByTestId(ids.stateMenu);
  await expect(menu.getByTestId(touchTestIds.actionSheetGroup)).toHaveText([
    'Proposed',
    'In progress',
    'Resolved',
    'Completed',
    'Removed',
  ]);
  const options = menu.getByTestId(ids.stateMenuOption);
  await expect(options).toHaveText(['New', 'Ready', 'Active✓', 'Resolved', 'Closed', 'Removed']);
  await expect(options.nth(2)).toHaveAttribute('aria-current', 'true');
  await expect(options.nth(2)).toHaveAttribute('aria-selected', 'true');
  await expect(options.nth(0)).not.toHaveAttribute('aria-current');
  await expect(menu.getByTestId(touchTestIds.actionSheetFooter)).toHaveText(
    'States for each type are set per project in Settings. The colour follows the category.',
  );
  for (const box of await options.evaluateAll((els) =>
    els.map((el) => el.getBoundingClientRect().toJSON()),
  )) {
    expect(box.height).toBeGreaterThanOrEqual(32);
  }
  await options.nth(4).tap();
  expect(picked).toEqual(['done']);
});

test('StateMenu_WhenGivenSubtitleAndFooter_ShouldShowThemInsteadOfTheFootnote', async ({
  mount,
  page,
}) => {
  await mount(
    <Sheet open title="State" onClose={() => undefined}>
      <StateMenu
        states={states}
        currentId="new"
        title="State of SHIP-141"
        subtitle="Pay with saved card"
        footer="Closed moves it off the board."
        onSelect={() => undefined}
        onClose={() => undefined}
      />
    </Sheet>,
  );
  const menu = page.getByTestId(ids.stateMenu);
  await expect(menu).toHaveAccessibleName('State of SHIP-141');
  await expect(menu).toContainText('Pay with saved card');
  await expect(menu.getByTestId(touchTestIds.actionSheetFooter)).toHaveText(
    'Closed moves it off the board.',
  );
});

for (const theme of THEMES) {
  test(`StateMenu_WhenInASheet_ShouldMatchItsScreenshot_state-menu-sheet_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(
      <Sheet open title="State" onClose={() => undefined}>
        <StateMenu
          states={states}
          currentId="active"
          onSelect={() => undefined}
          onClose={() => undefined}
        />
      </Sheet>,
    );
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.stateMenuOption)).toHaveCount(6);
    await expect(sheet).toHaveScreenshot(`state-menu-sheet.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
