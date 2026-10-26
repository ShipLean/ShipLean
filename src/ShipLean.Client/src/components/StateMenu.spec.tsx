import { expect, test } from '@playwright/experimental-ct-react';
import { StateMenu } from './StateMenu';
import { componentTestIds as ids } from './testIds';

const states = [
  { id: 'new', name: 'New', category: 'proposed' as const },
  { id: 'active', name: 'Active', category: 'inProgress' as const },
  { id: 'done', name: 'Done', category: 'completed' as const },
];

test('StateMenu_WhenStatePicked_ShouldCallOnSelect', async ({ mount, page }) => {
  const picked: string[] = [];
  await mount(
    <StateMenu
      states={states}
      currentId="new"
      onSelect={(id) => picked.push(id)}
      onClose={() => undefined}
    />,
  );
  await page.getByTestId(ids.stateMenuOption).nth(1).click();
  expect(picked).toEqual(['active']);
});

test('StateMenu_WhenArrowKeysUsed_ShouldMoveSelection', async ({ mount, page }) => {
  const picked: string[] = [];
  await mount(
    <StateMenu
      states={states}
      currentId="new"
      onSelect={(id) => picked.push(id)}
      onClose={() => undefined}
    />,
  );
  const options = page.getByTestId(ids.stateMenuOption);
  await expect(page.getByTestId(ids.stateMenu)).toBeFocused();
  await expect(options.nth(0)).toHaveAttribute('data-active', 'true');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await expect(options.nth(2)).toHaveAttribute('data-active', 'true');
  await page.keyboard.press('ArrowUp');
  await expect(options.nth(1)).toHaveAttribute('data-active', 'true');
  await page.keyboard.press('Enter');
  expect(picked).toEqual(['active']);
});

test('StateMenu_WhenEscPressed_ShouldClose', async ({ mount, page }) => {
  let closed = 0;
  await mount(
    <StateMenu states={states} onSelect={() => undefined} onClose={() => (closed += 1)} />,
  );
  await expect(page.getByTestId(ids.stateMenu)).toBeFocused();
  await page.keyboard.press('Escape');
  expect(closed).toBe(1);
});

test('StateMenu_WhenShown_ShouldNotShowTheRawCategory', async ({ mount, page }) => {
  await mount(<StateMenu states={states} onSelect={() => undefined} onClose={() => undefined} />);
  await expect(page.getByTestId(ids.stateMenuOption).nth(1)).toContainText('In progress');
  await expect(page.getByTestId(ids.stateMenu)).not.toContainText('inProgress');
});
