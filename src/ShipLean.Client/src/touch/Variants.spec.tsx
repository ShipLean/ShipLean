import { expect, test } from '@playwright/experimental-ct-react';
import { VariantsHarness } from './ct/VariantsHarness';
import { harnessTestIds as harness } from './ct/harnessIds';

// K1: `touch:` applies at 900px and below, `phone:` under 600px, and every text input is 16px at touch.
const at = (width: number) => ({ width, height: 800 });
const weight = (el: Element) => getComputedStyle(el).fontWeight;
const size = (el: Element) => getComputedStyle(el).fontSize;

const inputs = [
  harness.ctFieldInput,
  harness.ctUtilityInput,
  harness.ctInlineInput,
  harness.ctTextarea,
  harness.ctSelect,
  harness.ctCheckbox,
  harness.ctRadio,
];

test('Variants_WhenAt900_ShouldApplyTouch', async ({ mount, page }) => {
  await page.setViewportSize(at(900));
  await mount(<VariantsHarness />);
  expect(await page.getByTestId(harness.ctTouchProbe).evaluate(weight)).toBe('700');
});

test('Variants_WhenAt901_ShouldNotApplyTouch', async ({ mount, page }) => {
  await page.setViewportSize(at(901));
  await mount(<VariantsHarness />);
  expect(await page.getByTestId(harness.ctTouchProbe).evaluate(weight)).toBe('400');
});

test('Variants_WhenAt599_ShouldApplyPhone', async ({ mount, page }) => {
  await page.setViewportSize(at(599));
  await mount(<VariantsHarness />);
  expect(await page.getByTestId(harness.ctPhoneProbe).evaluate(weight)).toBe('700');
});

test('Variants_WhenAt600_ShouldNotApplyPhone', async ({ mount, page }) => {
  await page.setViewportSize(at(600));
  await mount(<VariantsHarness />);
  expect(await page.getByTestId(harness.ctPhoneProbe).evaluate(weight)).toBe('400');
  expect(await page.getByTestId(harness.ctTouchProbe).evaluate(weight)).toBe('700');
});

test('Inputs_WhenAtTouch_ShouldBe16px', async ({ mount, page }) => {
  await page.setViewportSize(at(900));
  await mount(<VariantsHarness />);
  for (const id of inputs) {
    expect(await page.getByTestId(id).evaluate(size), id).toBe('16px');
  }
});

test('Inputs_WhenAbove900_ShouldKeepTheirSize', async ({ mount, page }) => {
  await page.setViewportSize(at(901));
  await mount(<VariantsHarness />);
  const textSm = await page.evaluate(() =>
    getComputedStyle(document.documentElement).getPropertyValue('--text-sm').trim(),
  );
  expect(await page.getByTestId(harness.ctFieldInput).evaluate(size)).toBe(textSm);
  expect(await page.getByTestId(harness.ctUtilityInput).evaluate(size)).toBe(textSm);
  expect(await page.getByTestId(harness.ctInlineInput).evaluate(size)).toBe('12px');
  expect(await page.getByTestId(harness.ctTextarea).evaluate(size)).toBe(textSm);
  expect(await page.getByTestId(harness.ctSelect).evaluate(size)).toBe(textSm);
});
