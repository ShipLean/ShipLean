import { expect, test } from '@playwright/experimental-ct-react';
import { ButtonStatesHarness, type ButtonClass } from './ct/ButtonStatesHarness';
import { buttonHarnessIds as ids } from './ct/buttonHarnessIds';

// The shared button classes when disabled (K3, RETRO-1): faded, the default cursor and no hover change, for :disabled
// and aria-disabled, light and dark. The shot puts the enabled button beside the disabled ones, so a disabled button
// that looks enabled shows as twins. Transitions are switched off so the colour read after hover is the final one.

const THEMES = ['light', 'dark'] as const;

type Locator = ReturnType<Parameters<Parameters<typeof test>[2]>[0]['page']['getByTestId']>;

const colours = (button: Locator) =>
  button.evaluate((element) => {
    const style = getComputedStyle(element);
    return `${style.backgroundColor} ${style.color}`;
  });

const CASES: { name: string; variant: ButtonClass; shot: string }[] = [
  { name: 'ButtonSignal', variant: 'btn-signal', shot: 'button-signal' },
  { name: 'ButtonGhost', variant: 'btn-ghost', shot: 'button-ghost' },
  { name: 'ButtonDangerGhost', variant: 'btn-danger-ghost', shot: 'button-danger-ghost' },
];

for (const { name, variant, shot } of CASES) {
  for (const theme of THEMES) {
    test(`${name}_WhenDisabled_ShouldLookDisabled_${theme}`, async ({ mount, page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await mount(<ButtonStatesHarness variant={variant} />);
      await page.addStyleTag({
        content: '*, *::before, *::after { transition: none !important; }',
      });

      await expect(page.getByTestId(ids.ctButtonEnabled)).toHaveCSS('opacity', '1');
      await expect(page.getByTestId(ids.ctButtonDisabled)).toBeDisabled();
      for (const id of [ids.ctButtonDisabled, ids.ctButtonAriaDisabled]) {
        const button = page.getByTestId(id);
        await expect(button).toHaveCSS('opacity', '0.5');
        await expect(button).toHaveCSS('cursor', 'default');
        // Hover leaves a disabled button at its resting colours.
        const resting = await colours(button);
        await button.hover();
        expect(await colours(button)).toBe(resting);
        await page.mouse.move(0, 0);
      }
      await expect(page.getByTestId(ids.ctButtonStates)).toHaveScreenshot(`${shot}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}
