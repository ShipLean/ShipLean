import { expect, test } from '@playwright/experimental-ct-react';
type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const LIGHT_INK = 'rgb(28, 38, 56)';
const INDEX_PAPER = 'rgb(255, 255, 255)';
const POSTIT_PAPER = 'rgb(255, 249, 211)';
const BUG_PAPER = 'rgb(255, 224, 229)';
const DARK_ACCENT = '#5cc8f0';
const LIGHT_ACCENT = '#2563c9';

const setTheme = (page: Page, theme: 'light' | 'dark') =>
  page.evaluate((value) => document.documentElement.setAttribute('data-theme', value), theme);

const styleOf = (page: Page, testId: string) =>
  page.getByTestId(testId).evaluate((el) => {
    const style = getComputedStyle(el);
    return {
      color: style.color,
      background: style.backgroundColor,
      accent: style.getPropertyValue('--color-accent').trim(),
      themeAccent: style.getPropertyValue('--theme-accent').trim(),
    };
  });

for (const theme of ['light', 'dark'] as const) {
  const label = theme === 'dark' ? 'Dark' : 'Light';
  test(`PaperSheet_When${label}_ShouldDrawIndexPaperWithLightInk`, async ({ mount, page }) => {
    await mount(
      <div data-testid="paper" className="paper paper-sheet border bg-panel text-ink">
        Story card
      </div>,
    );
    await setTheme(page, theme);

    const style = await styleOf(page, 'paper');
    expect(style.color).toBe(LIGHT_INK);
    expect(style.background).toBe(INDEX_PAPER);
    expect(style.accent).toBe(LIGHT_ACCENT);
    expect(style.themeAccent).toBe(theme === 'dark' ? DARK_ACCENT : LIGHT_ACCENT);
  });

  test(`PaperPostit_When${label}_ShouldDrawPostitPaperWithLightInk`, async ({ mount, page }) => {
    await mount(
      <div data-testid="postit" className="paper paper-postit paper-sheet border">
        Task card
      </div>,
    );
    await setTheme(page, theme);

    const style = await styleOf(page, 'postit');
    expect(style.color).toBe(LIGHT_INK);
    expect(style.background).toBe(POSTIT_PAPER);
  });

  test(`PaperBug_When${label}_ShouldDrawPinkPaper`, async ({ mount, page }) => {
    await mount(
      <div data-testid="bug" className="paper paper-bug paper-sheet border">
        Bug card
      </div>,
    );
    await setTheme(page, theme);

    expect((await styleOf(page, 'bug')).background).toBe(BUG_PAPER);
  });
}

test('PaperSheet_WhenDarkAndNested_ShouldGiveChildUtilitiesPaperAndInk', async ({
  mount,
  page,
}) => {
  await mount(
    <div className="paper paper-sheet border">
      <span data-testid="inner" className="bg-panel text-ink">
        Inner
      </span>
    </div>,
  );
  await setTheme(page, 'dark');

  const style = await styleOf(page, 'inner');
  expect(style.color).toBe(LIGHT_INK);
  expect(style.background).toBe(INDEX_PAPER);
});
