import { expect, test } from '@playwright/experimental-ct-react';
import { TabBandHarness } from './ct/TabBandHarness';

test('TabBand_WhenTabClicked_ShouldCallOnChangeAndMarkIt', async ({ mount, page }) => {
  const changed: string[] = [];
  await mount(<TabBandHarness onChanged={(id) => changed.push(id)} />);
  await expect(page.getByTestId('band-source')).toHaveAttribute('aria-selected', 'true');

  await page.getByTestId('band-rendered').click();

  await expect(page.getByTestId('band-rendered')).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId('band-source')).toHaveAttribute('aria-selected', 'false');
  expect(changed).toEqual(['rendered']);
});

test('TabBand_WhenCountAndAside_ShouldShowThem', async ({ mount, page }) => {
  await mount(<TabBandHarness onChanged={() => undefined} />);

  await expect(page.getByTestId('band-history')).toHaveText('History2');
  await expect(page.getByTestId('band-source')).toHaveText('Source');
  await expect(page.getByTestId('band-aside')).toHaveText('Started from the built-in of 14 Nov');
});

test('TabBand_WhenOnAPhone_ShouldLeftAlignItsAside', async ({ mount, page }) => {
  await page.setViewportSize({ width: 500, height: 800 });
  await mount(<TabBandHarness onChanged={() => undefined} />);

  const history = await page.getByTestId('band-history').boundingBox();
  const aside = await page.getByTestId('band-aside').boundingBox();
  // Not pushed to the right edge: it follows the last tab (or starts the next row).
  expect(aside?.x).toBeLessThan((history?.x ?? 0) + (history?.width ?? 0) + 20);
});

test('TabBand_WhenArrowRightPressed_ShouldMoveToTheNextTab', async ({ mount, page }) => {
  await mount(<TabBandHarness onChanged={() => undefined} />);
  await page.getByTestId('band-source').focus();

  await page.keyboard.press('ArrowRight');

  await expect(page.getByTestId('band-rendered')).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId('band-rendered')).toBeFocused();
});
